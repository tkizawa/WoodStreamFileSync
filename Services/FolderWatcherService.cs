using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WoodStreamFileSync.Services;

public class FolderWatcherService : IDisposable
{
    private FileSystemWatcher? _watcher;
    private Timer? _debounceTimer;
    private readonly object _lock = new();
    private bool _isDisposed;
    private string _currentPath = "";
    private int _debounceSeconds = 10;
    private bool _isEnabled;

    public event Action? ChangeDetectedAndSettled;
    public event Action<string>? WatcherErrorOccurred;

    public bool IsWatching => _watcher != null && _watcher.EnableRaisingEvents;

    public void Start(string sourcePath, int debounceSeconds)
    {
        lock (_lock)
        {
            Stop();

            _currentPath = sourcePath;
            _debounceSeconds = Math.Max(1, debounceSeconds);
            _isEnabled = true;

            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                LoggerService.Instance.LogWarning("同期元フォルダが指定されていないため、リアルタイム監視を開始できません。", "Watcher");
                return;
            }

            if (!Directory.Exists(sourcePath))
            {
                LoggerService.Instance.LogWarning($"同期元フォルダ ({sourcePath}) が存在しないため、再試行待機を開始します。", "Watcher");
                StartReconnectWatcher();
                return;
            }

            try
            {
                _watcher = new FileSystemWatcher(sourcePath)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName
                                 | NotifyFilters.DirectoryName
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.Size
                                 | NotifyFilters.CreationTime
                };

                _watcher.Changed += OnFileSystemEvent;
                _watcher.Created += OnFileSystemEvent;
                _watcher.Deleted += OnFileSystemEvent;
                _watcher.Renamed += OnRenamedEvent;
                _watcher.Error += OnWatcherError;

                _watcher.EnableRaisingEvents = true;

                LoggerService.Instance.LogInfo($"リアルタイム監視を開始しました: {sourcePath} (待機秒数: {_debounceSeconds}秒)", "Watcher");
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogError($"監視の開始に失敗しました: {ex.Message}", "Watcher");
                StartReconnectWatcher();
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _isEnabled = false;
            _debounceTimer?.Dispose();
            _debounceTimer = null;

            if (_watcher != null)
            {
                try
                {
                    _watcher.EnableRaisingEvents = false;
                    _watcher.Changed -= OnFileSystemEvent;
                    _watcher.Created -= OnFileSystemEvent;
                    _watcher.Deleted -= OnFileSystemEvent;
                    _watcher.Renamed -= OnRenamedEvent;
                    _watcher.Error -= OnWatcherError;
                    _watcher.Dispose();
                }
                catch { }
                _watcher = null;
            }
        }
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        ResetDebounceTimer(e.FullPath, e.ChangeType.ToString());
    }

    private void OnRenamedEvent(object sender, RenamedEventArgs e)
    {
        ResetDebounceTimer(e.FullPath, $"Renamed from {e.OldName}");
    }

    private void ResetDebounceTimer(string path, string reason)
    {
        lock (_lock)
        {
            if (!_isEnabled) return;

            LoggerService.Instance.LogDebug($"変更検知: {path} ({reason}) - デバウンス待機開始 ({_debounceSeconds}秒)", "Watcher");

            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(OnDebounceTimerElapsed, null, TimeSpan.FromSeconds(_debounceSeconds), Timeout.InfiniteTimeSpan);
        }
    }

    private void OnDebounceTimerElapsed(object? state)
    {
        lock (_lock)
        {
            if (!_isEnabled) return;
            _debounceTimer?.Dispose();
            _debounceTimer = null;
        }

        LoggerService.Instance.LogInfo("変更後の待機時間が経過したため、自動同期をトリガーします。", "Watcher");
        try
        {
            ChangeDetectedAndSettled?.Invoke();
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"デバウンス同期呼出例外: {ex.Message}", "Watcher");
        }
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        var ex = e.GetException();
        LoggerService.Instance.LogWarning($"ファイル監視エラーまたは切断を検知しました: {ex?.Message}", "Watcher");
        WatcherErrorOccurred?.Invoke(ex?.Message ?? "Unknown error");

        lock (_lock)
        {
            if (_isEnabled)
            {
                Stop();
                _isEnabled = true;
                StartReconnectWatcher();
            }
        }
    }

    private void StartReconnectWatcher()
    {
        Task.Run(async () =>
        {
            while (_isEnabled && !_isDisposed)
            {
                await Task.Delay(15000); // 15秒ごとに再接続試行
                if (!_isEnabled || _isDisposed) break;

                if (!string.IsNullOrWhiteSpace(_currentPath) && Directory.Exists(_currentPath))
                {
                    LoggerService.Instance.LogInfo($"同期元フォルダの再接続を確認しました。監視を再開します: {_currentPath}", "Watcher");
                    Start(_currentPath, _debounceSeconds);
                    break;
                }
            }
        });
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        Stop();
    }
}
