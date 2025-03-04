﻿using BetterGenshinImpact.Core.Config;
using BetterGenshinImpact.GameTask;
using BetterGenshinImpact.GameTask.AutoDomain;
using BetterGenshinImpact.GameTask.AutoFight;
using BetterGenshinImpact.GameTask.AutoGeniusInvokation;
using BetterGenshinImpact.GameTask.AutoMusicGame;
using BetterGenshinImpact.GameTask.AutoSkip.Model;
using BetterGenshinImpact.GameTask.AutoTrackPath;
using BetterGenshinImpact.GameTask.AutoWood;
using BetterGenshinImpact.GameTask.Model;
using BetterGenshinImpact.Service.Interface;
using BetterGenshinImpact.View.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.System;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Violeta.Controls;

namespace BetterGenshinImpact.ViewModel.Pages;

public partial class TaskSettingsPageViewModel : ObservableObject, INavigationAware, IViewModel
{
    public AllConfig Config { get; set; }

    private readonly INavigationService _navigationService;
    private readonly TaskTriggerDispatcher _taskDispatcher;

    private CancellationTokenSource? _cts;
    private static readonly object _locker = new();

    [ObservableProperty] private string[] _strategyList;
    [ObservableProperty] private string _switchAutoGeniusInvokationButtonText = "启动";

    [ObservableProperty] private int _autoWoodRoundNum;
    [ObservableProperty] private int _autoWoodDailyMaxCount = 2000;
    [ObservableProperty] private string _switchAutoWoodButtonText = "启动";

    [ObservableProperty] private string[] _combatStrategyList;
    [ObservableProperty] private int _autoDomainRoundNum;
    [ObservableProperty] private string _switchAutoDomainButtonText = "启动";
    [ObservableProperty] private string _switchAutoFightButtonText = "启动";
    [ObservableProperty] private string _switchAutoTrackButtonText = "启动";
    [ObservableProperty] private string _switchAutoTrackPathButtonText = "启动";
    [ObservableProperty] private string _switchAutoMusicGameButtonText = "启动";

    public TaskSettingsPageViewModel(IConfigService configService, INavigationService navigationService, TaskTriggerDispatcher taskTriggerDispatcher)
    {
        Config = configService.Get();
        _navigationService = navigationService;
        _taskDispatcher = taskTriggerDispatcher;

        _strategyList = LoadCustomScript(Global.Absolute(@"User\AutoGeniusInvokation"));

        _combatStrategyList = ["根据队伍自动选择", .. LoadCustomScript(Global.Absolute(@"User\AutoFight"))];
    }

    private string[] LoadCustomScript(string folder)
    {
        var files = Directory.GetFiles(folder, "*.*",
            SearchOption.AllDirectories);

        var strategyList = new string[files.Length];
        for (var i = 0; i < files.Length; i++)
        {
            if (files[i].EndsWith(".txt"))
            {
                var strategyName = files[i].Replace(folder, "").Replace(".txt", "");
                if (strategyName.StartsWith('\\'))
                {
                    strategyName = strategyName[1..];
                }
                strategyList[i] = strategyName;
            }
        }

        return strategyList;
    }

    [RelayCommand]
    private void OnStrategyDropDownOpened(string type)
    {
        switch (type)
        {
            case "Combat":
                CombatStrategyList = ["根据队伍自动选择", .. LoadCustomScript(Global.Absolute(@"User\AutoFight"))];
                break;

            case "GeniusInvocation":
                StrategyList = LoadCustomScript(Global.Absolute(@"User\AutoGeniusInvokation"));
                break;
        }
    }

    public void OnNavigatedTo()
    {
    }

    public void OnNavigatedFrom()
    {
    }

    [RelayCommand]
    public void OnGoToHotKeyPage()
    {
        _navigationService.Navigate(typeof(HotKeyPage));
    }

    [RelayCommand]
    public void OnSwitchAutoGeniusInvokation()
    {
        try
        {
            lock (_locker)
            {
                if (SwitchAutoGeniusInvokationButtonText == "启动")
                {
                    if (string.IsNullOrEmpty(Config.AutoGeniusInvokationConfig.StrategyName))
                    {
                        Toast.Warning("请先选择策略");
                        return;
                    }

                    var path = Global.Absolute(@"User\AutoGeniusInvokation\" + Config.AutoGeniusInvokationConfig.StrategyName + ".txt");

                    if (!File.Exists(path))
                    {
                        Toast.Error("策略文件不存在");
                        return;
                    }

                    var content = File.ReadAllText(path);
                    _cts?.Cancel();
                    _cts = new CancellationTokenSource();
                    var param = new GeniusInvokationTaskParam(_cts, content);
                    _taskDispatcher.StartIndependentTask(IndependentTaskEnum.AutoGeniusInvokation, param);
                    SwitchAutoGeniusInvokationButtonText = "停止";
                }
                else
                {
                    _cts?.Cancel();
                    SwitchAutoGeniusInvokationButtonText = "启动";
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Error(ex.Message);
        }
    }

    [RelayCommand]
    public async Task OnGoToAutoGeniusInvokationUrlAsync()
    {
        await Launcher.LaunchUriAsync(new Uri("https://bgi.huiyadan.com/doc.html#%E8%87%AA%E5%8A%A8%E4%B8%83%E5%9C%A3%E5%8F%AC%E5%94%A4"));
    }

    [RelayCommand]
    public void OnSwitchAutoWood()
    {
        try
        {
            lock (_locker)
            {
                if (SwitchAutoWoodButtonText == "启动")
                {
                    _cts?.Cancel();
                    _cts = new CancellationTokenSource();
                    var param = new WoodTaskParam(_cts, AutoWoodRoundNum, AutoWoodDailyMaxCount);
                    _taskDispatcher.StartIndependentTask(IndependentTaskEnum.AutoWood, param);
                    SwitchAutoWoodButtonText = "停止";
                }
                else
                {
                    _cts?.Cancel();
                    SwitchAutoWoodButtonText = "启动";
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Error(ex.Message);
        }
    }

    [RelayCommand]
    public async Task OnGoToAutoWoodUrlAsync()
    {
        await Launcher.LaunchUriAsync(new Uri("https://bgi.huiyadan.com/feats/felling.html"));
    }

    [RelayCommand]
    public void OnSwitchAutoFight()
    {
        try
        {
            lock (_locker)
            {
                if (SwitchAutoFightButtonText == "启动")
                {
                    var path = Global.Absolute(@"User\AutoFight\" + Config.AutoFightConfig.StrategyName + ".txt");
                    if ("根据队伍自动选择".Equals(Config.AutoFightConfig.StrategyName))
                    {
                        path = Global.Absolute(@"User\AutoFight\");
                    }
                    if (!File.Exists(path) && !Directory.Exists(path))
                    {
                        Toast.Error("战斗策略文件不存在");
                        return;
                    }

                    _cts?.Cancel();
                    _cts = new CancellationTokenSource();
                    var param = new AutoFightParam(_cts, path);
                    _taskDispatcher.StartIndependentTask(IndependentTaskEnum.AutoFight, param);
                    SwitchAutoFightButtonText = "停止";
                }
                else
                {
                    _cts?.Cancel();
                    SwitchAutoFightButtonText = "启动";
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Error(ex.Message);
        }
    }

    [Obsolete]
    private string? ReadFightStrategy(string strategyName)
    {
        if (string.IsNullOrEmpty(strategyName))
        {
            Toast.Warning("请先选择战斗策略");
            return null;
        }

        var path = Global.Absolute(@"User\AutoFight\" + strategyName + ".txt");

        if (!File.Exists(path))
        {
            Toast.Error("战斗策略文件不存在");
            return null;
        }

        var content = File.ReadAllText(path);
        return content;
    }

    [RelayCommand]
    public async Task OnGoToAutoFightUrlAsync()
    {
        await Launcher.LaunchUriAsync(new Uri("https://bgi.huiyadan.com/feats/domain.html"));
    }

    [RelayCommand]
    public void OnSwitchAutoDomain()
    {
        try
        {
            lock (_locker)
            {
                if (SwitchAutoDomainButtonText == "启动")
                {
                    var path = Global.Absolute(@"User\AutoFight\" + Config.AutoFightConfig.StrategyName + ".txt");
                    if ("根据队伍自动选择".Equals(Config.AutoFightConfig.StrategyName))
                    {
                        path = Global.Absolute(@"User\AutoFight\");
                    }
                    if (!File.Exists(path) && !Directory.Exists(path))
                    {
                        Toast.Error("战斗策略文件不存在");
                        return;
                    }

                    _cts?.Cancel();
                    _cts = new CancellationTokenSource();
                    var param = new AutoDomainParam(_cts, AutoDomainRoundNum, path);
                    _taskDispatcher.StartIndependentTask(IndependentTaskEnum.AutoDomain, param);
                    SwitchAutoDomainButtonText = "停止";
                }
                else
                {
                    _cts?.Cancel();
                    SwitchAutoDomainButtonText = "启动";
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Error(ex.Message);
        }
    }

    [RelayCommand]
    public async Task OnGoToAutoDomainUrlAsync()
    {
        await Launcher.LaunchUriAsync(new Uri("https://bgi.huiyadan.com/feats/domain.html"));
    }

    [RelayCommand]
    public void OnOpenFightFolder()
    {
        Process.Start("explorer.exe", Global.Absolute(@"User\AutoFight\"));
    }

    [RelayCommand]
    public void OnSwitchAutoTrack()
    {
        try
        {
            lock (_locker)
            {
                if (SwitchAutoTrackButtonText == "启动")
                {
                    _cts?.Cancel();
                    _cts = new CancellationTokenSource();
                    var param = new AutoTrackParam(_cts);
                    _taskDispatcher.StartIndependentTask(IndependentTaskEnum.AutoTrack, param);
                    SwitchAutoTrackButtonText = "停止";
                }
                else
                {
                    _cts?.Cancel();
                    SwitchAutoTrackButtonText = "启动";
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Error(ex.Message);
        }
    }

    [RelayCommand]
    public async Task OnGoToAutoTrackUrlAsync()
    {
        await Launcher.LaunchUriAsync(new Uri("https://bgi.huiyadan.com/feats/track.html"));
    }

    [RelayCommand]
    public void OnSwitchAutoTrackPath()
    {
        try
        {
            lock (_locker)
            {
                if (SwitchAutoTrackPathButtonText == "启动")
                {
                    _cts?.Cancel();
                    _cts = new CancellationTokenSource();
                    var param = new AutoTrackPathParam(_cts);
                    _taskDispatcher.StartIndependentTask(IndependentTaskEnum.AutoTrackPath, param);
                    SwitchAutoTrackPathButtonText = "停止";
                }
                else
                {
                    _cts?.Cancel();
                    SwitchAutoTrackPathButtonText = "启动";
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Error(ex.Message);
        }
    }

    [RelayCommand]
    public async Task OnGoToAutoTrackPathUrlAsync()
    {
        await Launcher.LaunchUriAsync(new Uri("https://bgi.huiyadan.com/feats/track.html"));
    }

    [RelayCommand]
    public void OnSwitchAutoMusicGame()
    {
        try
        {
            lock (_locker)
            {
                if (SwitchAutoMusicGameButtonText == "启动")
                {
                    _cts?.Cancel();
                    _cts = new CancellationTokenSource();
                    var param = new AutoMusicGameParam(_cts);
                    _taskDispatcher.StartIndependentTask(IndependentTaskEnum.AutoMusicGame, param);
                    SwitchAutoMusicGameButtonText = "停止";
                }
                else
                {
                    _cts?.Cancel();
                    SwitchAutoMusicGameButtonText = "启动";
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Error(ex.Message);
        }
    }

    [RelayCommand]
    public async Task OnGoToAutoMusicGameUrlAsync()
    {
        await Launcher.LaunchUriAsync(new Uri("https://bgi.huiyadan.com/feats/music.html"));
    }

    public static void SetSwitchAutoTrackButtonText(bool running)
    {
        var instance = App.GetService<TaskSettingsPageViewModel>();
        if (instance == null)
        {
            return;
        }

        instance.SwitchAutoTrackButtonText = running ? "停止" : "启动";
    }

    public static void SetSwitchAutoGeniusInvokationButtonText(bool running)
    {
        var instance = App.GetService<TaskSettingsPageViewModel>();
        if (instance == null)
        {
            return;
        }

        instance.SwitchAutoGeniusInvokationButtonText = running ? "停止" : "启动";
    }

    public static void SetSwitchAutoWoodButtonText(bool running)
    {
        var instance = App.GetService<TaskSettingsPageViewModel>();
        if (instance == null)
        {
            return;
        }

        instance.SwitchAutoWoodButtonText = running ? "停止" : "启动";
    }

    public static void SetSwitchAutoDomainButtonText(bool running)
    {
        var instance = App.GetService<TaskSettingsPageViewModel>();
        if (instance == null)
        {
            return;
        }

        instance.SwitchAutoDomainButtonText = running ? "停止" : "启动";
    }

    public static void SetSwitchAutoFightButtonText(bool running)
    {
        var instance = App.GetService<TaskSettingsPageViewModel>();
        if (instance == null)
        {
            return;
        }

        instance.SwitchAutoFightButtonText = running ? "停止" : "启动";
    }
}
