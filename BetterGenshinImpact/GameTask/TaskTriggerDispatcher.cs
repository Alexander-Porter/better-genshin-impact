﻿using BetterGenshinImpact.Core.Config;
using BetterGenshinImpact.GameTask.AutoDomain;
using BetterGenshinImpact.GameTask.AutoFight;
using BetterGenshinImpact.GameTask.AutoGeniusInvokation;
using BetterGenshinImpact.GameTask.AutoMusicGame;
using BetterGenshinImpact.GameTask.AutoSkip;
using BetterGenshinImpact.GameTask.AutoSkip.Model;
using BetterGenshinImpact.GameTask.AutoTrackPath;
using BetterGenshinImpact.GameTask.AutoWood;
using BetterGenshinImpact.GameTask.Common;
using BetterGenshinImpact.GameTask.Model;
using BetterGenshinImpact.GameTask.Model.Area;
using BetterGenshinImpact.GameTask.Model.Enum;
using BetterGenshinImpact.Helpers;
using BetterGenshinImpact.View;
using Fischless.GameCapture;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vanara.PInvoke;

namespace BetterGenshinImpact.GameTask
{
    public class TaskTriggerDispatcher : IDisposable
    {
        private readonly ILogger<TaskTriggerDispatcher> _logger = App.GetLogger<TaskTriggerDispatcher>();

        private static TaskTriggerDispatcher? _instance;

        private readonly System.Timers.Timer _timer = new();
        private List<ITaskTrigger>? _triggers;

        public IGameCapture? GameCapture { get; private set; }

        private static readonly object _locker = new();
        private int _frameIndex = 0;

        private RECT _gameRect = RECT.Empty;
        private bool _prevGameActive;

        private DateTime _prevManualGc = DateTime.MinValue;

        /// <summary>
        /// 捕获结果队列
        /// </summary>
        private Bitmap _bitmap = new(10, 10);

        /// <summary>
        /// 调度器捕获模式, 影响以下内容：
        /// 1. 是否缓存图像
        /// 2. 是否执行触发器
        /// </summary>
        private DispatcherCaptureModeEnum _dispatcherCacheCaptureMode = DispatcherCaptureModeEnum.NormalTrigger;

        private static readonly object _bitmapLocker = new();
        private static readonly object _triggerListLocker = new();

        public event EventHandler? UiTaskStopTickEvent;

        public event EventHandler? UiTaskStartTickEvent;

        public TaskTriggerDispatcher()
        {
            _instance = this;
            _timer.Elapsed += Tick;
            //_timer.Tick += Tick;
        }

        public static TaskTriggerDispatcher Instance()
        {
            if (_instance == null)
            {
                throw new Exception("请先在启动页启动BetterGI，如果已经启动请重启");
            }

            return _instance;
        }

        public static IGameCapture GlobalGameCapture
        {
            get
            {
                _instance = Instance();

                if (_instance.GameCapture == null)
                {
                    throw new Exception("截图器未初始化!");
                }

                return _instance.GameCapture;
            }
        }

        public void ClearTriggers()
        {
            lock (_triggerListLocker)
            {
                GameTaskManager.ClearTriggers();
                _triggers?.Clear();
            }
        }

        public void SetTriggers(List<ITaskTrigger> list)
        {
            lock (_triggerListLocker)
            {
                _triggers = list;
            }
        }

        public void AddTrigger(string name, object? externalConfig)
        {
            lock (_triggerListLocker)
            {
                GameTaskManager.AddTrigger(name, externalConfig);
                SetTriggers(GameTaskManager.ConvertToTriggerList(true));
            }
        }

        public void Start(IntPtr hWnd, CaptureModes mode, int interval = 50)
        {
            // 初始化截图器
            GameCapture = GameCaptureFactory.Create(mode);
            // 激活窗口 保证后面能够正常获取窗口信息
            SystemControl.ActivateWindow(hWnd);

            // 初始化任务上下文(一定要在初始化触发器前完成)
            TaskContext.Instance().Init(hWnd);

            // 初始化触发器(一定要在任务上下文初始化完毕后使用)
            _triggers = GameTaskManager.LoadInitialTriggers();

            // 启动截图
            GameCapture.Start(hWnd,
                new Dictionary<string, object>()
                {
                    { "useBitmapCache", TaskContext.Instance().Config.WgcUseBitmapCache }
                }
            );

            // 捕获模式初始化配置
            if (TaskContext.Instance().Config.CommonConfig.ScreenshotEnabled || TaskContext.Instance().Config.MacroConfig.CombatMacroEnabled)
            {
                _dispatcherCacheCaptureMode = DispatcherCaptureModeEnum.CacheCaptureWithTrigger;
            }

            // 启动定时器
            _frameIndex = 0;
            _timer.Interval = interval;
            if (!_timer.Enabled)
            {
                _timer.Start();
            }
        }

        public void Stop()
        {
            _timer.Stop();
            GameCapture?.Stop();
            _gameRect = RECT.Empty;
            _prevGameActive = false;
        }

        public void StartTimer()
        {
            if (!_timer.Enabled)
            {
                _timer.Start();
            }
        }

        public void StopTimer()
        {
            if (_timer.Enabled)
            {
                _timer.Stop();
            }
        }

        /// <summary>
        /// 启动独立任务
        /// </summary>
        public void StartIndependentTask(IndependentTaskEnum taskType, BaseTaskParam param)
        {
            if (!_timer.Enabled)
            {
                throw new Exception("请先在启动页启动BetterGI，如果已经启动请重启");
            }

            var maskWindow = MaskWindow.Instance();
            maskWindow.Invoke(() => { maskWindow.Show(); });
            if (taskType == IndependentTaskEnum.AutoGeniusInvokation)
            {
                AutoGeniusInvokationTask.Start((GeniusInvokationTaskParam)param);
            }
            else if (taskType == IndependentTaskEnum.AutoWood)
            {
                Task.Run(() => { new AutoWoodTask().Start((WoodTaskParam)param); });
            }
            else if (taskType == IndependentTaskEnum.AutoFight)
            {
                Task.Run(() => { new AutoFightTask((AutoFightParam)param).Start(); });
            }
            else if (taskType == IndependentTaskEnum.AutoDomain)
            {
                Task.Run(() => { new AutoDomainTask((AutoDomainParam)param).Start(); });
            }
            else if (taskType == IndependentTaskEnum.AutoTrack)
            {
                Task.Run(() => { new AutoTrackTask((AutoTrackParam)param).Start(); });
            }
            else if (taskType == IndependentTaskEnum.AutoTrackPath)
            {
                Task.Run(() => { new AutoTrackPathTask((AutoTrackPathParam)param).Start(); });
            }
            else if (taskType == IndependentTaskEnum.AutoMusicGame)
            {
                Task.Run(() => { new AutoMusicGameTask((AutoMusicGameParam)param).Start(); });
            }
        }

        public void Dispose()
        {
            Stop();
        }

        public void Tick(object? sender, EventArgs e)
        {
            var hasLock = false;
            try
            {
                Monitor.TryEnter(_locker, ref hasLock);
                if (!hasLock)
                {
                    // 正在执行时跳过
                    return;
                }

                // 检查截图器是否初始化
                var maskWindow = MaskWindow.Instance();
                if (GameCapture == null || !GameCapture.IsCapturing)
                {
                    if (!TaskContext.Instance().SystemInfo.GameProcess.HasExited)
                    {
                        _logger.LogError("截图器未初始化!");
                    }
                    else
                    {
                        _logger.LogInformation("游戏已退出，BetterGI 自动停止截图器");
                    }

                    UiTaskStopTickEvent?.Invoke(sender, e);
                    maskWindow.Invoke(maskWindow.Hide);
                    return;
                }

                // 检查游戏是否在前台
                var hasBackgroundTriggerToRun = false;
                var active = SystemControl.IsGenshinImpactActive();
                if (!active)
                {
                    // 检查游戏是否已结束
                    if (TaskContext.Instance().SystemInfo.GameProcess.HasExited)
                    {
                        _logger.LogInformation("游戏已退出，BetterGI 自动停止截图器");
                        UiTaskStopTickEvent?.Invoke(sender, e);
                        return;
                    }

                    if (_prevGameActive)
                    {
                        Debug.WriteLine("游戏窗口不在前台, 不再进行截屏");
                    }

                    if (!TaskContext.Instance().Config.MaskWindowConfig.UseSubform)
                    {
                        var pName = SystemControl.GetActiveProcessName();
                        if (pName != "BetterGI" && pName != "YuanShen" && pName != "GenshinImpact" && pName != "Genshin Impact Cloud Game")
                        {
                            maskWindow.Invoke(() => { maskWindow.Hide(); });
                        }
                    }

                    _prevGameActive = active;

                    if (_triggers != null)
                    {
                        lock (_triggerListLocker)
                        {
                            var exclusive = _triggers.FirstOrDefault(t => t is { IsEnabled: true, IsExclusive: true });
                            if (exclusive != null)
                            {
                                hasBackgroundTriggerToRun = exclusive.IsBackgroundRunning;
                            }
                            else
                            {
                                hasBackgroundTriggerToRun = _triggers.Any(t => t is { IsEnabled: true, IsBackgroundRunning: true });
                            }
                        }
                    }

                    if (!hasBackgroundTriggerToRun)
                    {
                        // 没有后台运行的触发器，这次不再进行截图
                        return;
                    }
                }
                else
                {
                    if (!TaskContext.Instance().Config.MaskWindowConfig.UseSubform)
                    {
                        if (!_prevGameActive)
                        {
                            maskWindow.Invoke(() =>
                            {
                                if (maskWindow.IsExist())
                                {
                                    maskWindow.Show();
                                }
                            });
                        }
                    }

                    _prevGameActive = active;
                    // 移动游戏窗口的时候同步遮罩窗口的位置,此时不进行捕获
                    if (SyncMaskWindowPosition())
                    {
                        return;
                    }
                }

                // 帧序号自增 1分钟后归零(MaxFrameIndexSecond)
                _frameIndex = (_frameIndex + 1) % (int)(CaptureContent.MaxFrameIndexSecond * 1000d / _timer.Interval);

                if (_dispatcherCacheCaptureMode == DispatcherCaptureModeEnum.NormalTrigger
                    && (_triggers == null || !_triggers.Exists(t => t.IsEnabled)))
                {
                    // Debug.WriteLine("没有可用的触发器且不处于仅截屏状态, 不再进行截屏");
                    return;
                }

                var speedTimer = new SpeedTimer();
                // 捕获游戏画面
                var bitmap = GameCapture.Capture();
                speedTimer.Record("截图");

                if (bitmap == null)
                {
                    _logger.LogWarning("截图失败!");
                    return;
                }

                if (IsOnlyCacheCapture(bitmap))
                {
                    return;
                }

                // 循环执行所有触发器 有独占状态的触发器的时候只执行独占触发器
                var content = new CaptureContent(bitmap, _frameIndex, _timer.Interval);

                lock (_triggerListLocker)
                {
                    var exclusiveTrigger = _triggers!.FirstOrDefault(t => t is { IsEnabled: true, IsExclusive: true });
                    if (exclusiveTrigger != null)
                    {
                        exclusiveTrigger.OnCapture(content);
                        speedTimer.Record(exclusiveTrigger.Name);
                    }
                    else
                    {
                        var runningTriggers = _triggers!.Where(t => t.IsEnabled);
                        if (hasBackgroundTriggerToRun)
                        {
                            runningTriggers = runningTriggers.Where(t => t.IsBackgroundRunning);
                        }

                        foreach (var trigger in runningTriggers)
                        {
                            trigger.OnCapture(content);
                            speedTimer.Record(trigger.Name);
                        }
                    }
                }

                speedTimer.DebugPrint();
                content.Dispose();
            }
            finally
            {
                if ((DateTime.Now - _prevManualGc).TotalSeconds > 2)
                {
                    GC.Collect();
                    _prevManualGc = DateTime.Now;
                }

                if (hasLock)
                {
                    Monitor.Exit(_locker);
                }
            }
        }

        /// <summary>
        /// / 移动游戏窗口的时候同步遮罩窗口的位置
        /// </summary>
        /// <returns></returns>
        private bool SyncMaskWindowPosition()
        {
            var hWnd = TaskContext.Instance().GameHandle;
            var currentRect = SystemControl.GetCaptureRect(hWnd);
            if (_gameRect == RECT.Empty)
            {
                _gameRect = new RECT(currentRect);
            }
            else if (_gameRect != currentRect)
            {
                // 后面大概可以取消掉这个判断，支持随意移动变化窗口 —— 不支持 需要考虑的问题太多了
                if ((_gameRect.Width != currentRect.Width || _gameRect.Height != currentRect.Height)
                    && !SizeIsZero(_gameRect) && !SizeIsZero(currentRect))
                {
                    _logger.LogError("► 游戏窗口大小发生变化 {W}x{H}->{CW}x{CH}, 自动重启截图器中...", _gameRect.Width, _gameRect.Height, currentRect.Width, currentRect.Height);
                    UiTaskStopTickEvent?.Invoke(null, EventArgs.Empty);
                    UiTaskStartTickEvent?.Invoke(null, EventArgs.Empty);
                    _logger.LogInformation("► 游戏窗口大小发生变化，截图器重启完成！");
                }

                _gameRect = new RECT(currentRect);
                TaskContext.Instance().SystemInfo.CaptureAreaRect = currentRect;
                MaskWindow.Instance().RefreshPosition();
                return true;
            }

            return false;
        }

        private bool SizeIsZero(RECT rect)
        {
            return rect.Width == 0 || rect.Height == 0;
        }

        /// <summary>
        /// 是否仅缓存截图
        /// </summary>
        /// <param name="bitmap"></param>
        /// <returns></returns>
        private bool IsOnlyCacheCapture(Bitmap bitmap)
        {
            lock (_bitmapLocker)
            {
                if (_dispatcherCacheCaptureMode is DispatcherCaptureModeEnum.OnlyCacheCapture or DispatcherCaptureModeEnum.CacheCaptureWithTrigger)
                {
                    _bitmap = new Bitmap(bitmap);
                    if (_dispatcherCacheCaptureMode == DispatcherCaptureModeEnum.OnlyCacheCapture)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public void SetCacheCaptureMode(DispatcherCaptureModeEnum mode)
        {
            if (mode is DispatcherCaptureModeEnum.Start)
            {
                this.StartTimer();
            }
            else if (mode is DispatcherCaptureModeEnum.Stop)
            {
                this.StopTimer();
            }
            else
            {
                _dispatcherCacheCaptureMode = mode;
            }
        }

        public DispatcherCaptureModeEnum GetCacheCaptureMode()
        {
            return _dispatcherCacheCaptureMode;
        }

        public Bitmap GetLastCaptureBitmap()
        {
            lock (_bitmapLocker)
            {
                return new Bitmap(_bitmap);
            }
        }

        public CaptureContent GetLastCaptureContent()
        {
            var bitmap = GetLastCaptureBitmap();
            return new CaptureContent(bitmap, _frameIndex, _timer.Interval);
        }

        public ImageRegion CaptureToRectArea(bool forceNew = false)
        {
            // 触发器启动的情况下优先使用触发器的截图
            if (!forceNew && _timer.Enabled && _dispatcherCacheCaptureMode is DispatcherCaptureModeEnum.OnlyCacheCapture or DispatcherCaptureModeEnum.CacheCaptureWithTrigger)
            {
                return GetLastCaptureContent().CaptureRectArea;
            }
            else
            {
                var bitmap = TaskControl.CaptureGameBitmap(GameCapture);
                var content = new CaptureContent(bitmap, 0, 0);
                return content.CaptureRectArea;
            }
        }

        public void TakeScreenshot()
        {
            if (_dispatcherCacheCaptureMode is DispatcherCaptureModeEnum.OnlyCacheCapture or DispatcherCaptureModeEnum.CacheCaptureWithTrigger)
            {
                try
                {
                    var path = Global.Absolute($@"log\screenshot\");
                    if (!Directory.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                    }

                    var bitmap = GetLastCaptureBitmap();
                    var name = $@"{DateTime.Now:yyyyMMddHHmmssffff}.png";
                    var savePath = Global.Absolute($@"log\screenshot\{name}");

                    if (TaskContext.Instance().Config.CommonConfig.ScreenshotUidCoverEnabled)
                    {
                        var mat = bitmap.ToMat();
                        var rect = TaskContext.Instance().Config.MaskWindowConfig.UidCoverRect;
                        mat.Rectangle(rect, Scalar.White, -1);
                        Cv2.ImWrite(savePath, mat);
                    }
                    else
                    {
                        bitmap.Save(savePath, ImageFormat.Png);
                    }

                    _logger.LogInformation("截图已保存: {Name}", name);
                }
                catch (Exception e)
                {
                    _logger.LogError("截图保存失败: {Message}", e.Message);
                    _logger.LogDebug("截图保存失败: {StackTrace}", e.StackTrace);
                }
            }
            else
            {
                _logger.LogWarning("当前不处于截图模式，无法保存截图");
            }
        }
    }
}
