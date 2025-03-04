﻿using BetterGenshinImpact.Core.Config;
using BetterGenshinImpact.Core.Script.Project;
using BetterGenshinImpact.GameTask.AutoPathing.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BetterGenshinImpact.Core.Script;
using BetterGenshinImpact.GameTask;
using BetterGenshinImpact.GameTask.AutoPathing;
using BetterGenshinImpact.GameTask.Model.Enum;
using Wpf.Ui.Controls;
using Wpf.Ui.Violeta.Controls;

namespace BetterGenshinImpact.ViewModel.Pages;

public partial class MapPathingViewModel : ObservableObject, INavigationAware, IViewModel
{
    private readonly ILogger<MapPathingViewModel> _logger = App.GetLogger<MapPathingViewModel>();
    public static readonly string PathJsonPath = Global.Absolute(@"User\AutoPathing");

    [ObservableProperty]
    private ObservableCollection<PathingTask> _pathItems = [];

    private void InitScriptListViewData()
    {
        _pathItems.Clear();
        var fileInfos = LoadScriptFolder(PathJsonPath);
        foreach (var f in fileInfos)
        {
            try
            {
                _pathItems.Add(PathingTask.BuildFromFilePath(f.FullName));
            }
            catch (Exception e)
            {
                Toast.Warning($"地图追踪任务 {f.Name} 载入失败：{e.Message}");
            }
        }
    }

    private IEnumerable<FileInfo> LoadScriptFolder(string folder)
    {
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var files = Directory.GetFiles(folder, "*.*",
            SearchOption.AllDirectories);

        return files.Select(file => new FileInfo(file)).ToList();
    }

    public void OnNavigatedTo()
    {
        InitScriptListViewData();
    }

    public void OnNavigatedFrom()
    {
    }

    [RelayCommand]
    public void OnOpenScriptsFolder()
    {
        if (!Directory.Exists(PathJsonPath))
        {
            Directory.CreateDirectory(PathJsonPath);
        }
        Process.Start("explorer.exe", PathJsonPath);
    }

    [RelayCommand]
    public void OnOpenScriptProjectFolder(ScriptProject? item)
    {
        if (item == null)
        {
            return;
        }
        Process.Start("explorer.exe", item.ProjectPath);
    }

    [RelayCommand]
    public async Task OnStart(PathingTask? item)
    {
        if (item == null)
        {
            return;
        }
        await new TaskRunner(DispatcherTimerOperationEnum.UseSelfCaptureImage)
            .RunAsync(async () => await new PathExecutor(CancellationContext.Instance.Cts).Pathing(item));
    }
}
