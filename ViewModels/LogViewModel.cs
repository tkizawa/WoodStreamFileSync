using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using WoodStreamFileSync.Models;
using WoodStreamFileSync.Services;

namespace WoodStreamFileSync.ViewModels;

public class LogViewModel : ViewModelBase
{
    private readonly LoggerService _logger;
    private bool _autoScroll = true;
    private readonly ConcurrentQueue<SyncLogEntry> _pendingLogs = new();
    private readonly DispatcherTimer _uiUpdateTimer;

    public ObservableCollection<SyncLogEntry> Logs { get; } = new();

    public bool AutoScroll
    {
        get => _autoScroll;
        set => SetProperty(ref _autoScroll, value);
    }

    public ICommand ClearLogsCommand { get; }
    public ICommand CopyLogsCommand { get; }
    public ICommand OpenLogFolderCommand { get; }

    public LogViewModel(LoggerService logger)
    {
        _logger = logger;

        foreach (var entry in _logger.GetRecentLogs())
        {
            Logs.Add(entry);
        }

        _logger.LogReceived += OnLogReceived;

        // UIスレッドを飽和させないためのバッチ更新タイマー (100msごとにまとめてUIに追加)
        _uiUpdateTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _uiUpdateTimer.Tick += OnUiUpdateTick;
        _uiUpdateTimer.Start();

        ClearLogsCommand = new RelayCommand(ClearLogs);
        CopyLogsCommand = new RelayCommand(CopyLogs);
        OpenLogFolderCommand = new RelayCommand(OpenLogFolder);
    }

    private void OnLogReceived(SyncLogEntry entry)
    {
        _pendingLogs.Enqueue(entry);
    }

    private void OnUiUpdateTick(object? sender, EventArgs e)
    {
        if (_pendingLogs.IsEmpty) return;

        int addedCount = 0;
        while (_pendingLogs.TryDequeue(out var entry) && addedCount < 100)
        {
            Logs.Add(entry);
            addedCount++;
        }

        while (Logs.Count > 1500)
        {
            Logs.RemoveAt(0);
        }
    }

    private void ClearLogs()
    {
        _logger.ClearMemoryLogs();
        while (_pendingLogs.TryDequeue(out _)) { }
        Logs.Clear();
    }

    private void CopyLogs()
    {
        var sb = new StringBuilder();
        foreach (var entry in Logs)
        {
            sb.AppendLine(entry.ToString());
        }
        Clipboard.SetText(sb.ToString());
        MessageBox.Show("ログをクリップボードにコピーしました。", "コピー完了", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OpenLogFolder()
    {
        try
        {
            var dir = _logger.GetLogDirectory();
            if (Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ログフォルダを開けませんでした: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
