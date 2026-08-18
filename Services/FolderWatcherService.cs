using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WoodStreamFileSync.Services;

public class FolderWatcherService : IDisposable
{
    private class WatcherItem : IDisposable
    {
        public string Path { get; set; } = string.Empty;
        public FileSystemWatcher? Watcher { get; set; }
        public Timer? DebounceTimer { get; set; }
        public bool IsReconnecting { get; set; }

        public void Dispose()
        {
            DebounceTimer?.Dispose();
            DebounceTimer = null;
            if (Watcher != null)
            {
                try
                {
                    Watcher.EnableRaisingEvents = false;
                    Watcher.Dispose();
                }
                catch { }
                Watcher = null;
            }
        }
    }

    private readonly Dictionary<string, WatcherItem> _watchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private bool _isDisposed;
    private int _debounceSeconds = 10;
    private bool _isEnabled;

    public event Action? ChangeDetectedAndSettled;
    public event Action<string>? ChangeDetectedForPathAndSettled;
    public event Action<string>? WatcherErrorOccurred;

    public bool IsWatching
    {
        get
        {
            lock (_lock)
            {
                return _isEnabled && _watchers.Values.Any(w => w.Watcher != null && w.Watcher.EnableRaisingEvents);
            }
        }
    }

    public void Start(string sourcePath, int debounceSeconds)
    {
        var paths = string.IsNullOrWhiteSpace(sourcePath) ? Enumerable.Empty<string>() : new[] { sourcePath };
        Start(paths, debounceSeconds);
    }

    public void Start(IEnumerable<string> sourcePaths, int debounceSeconds)
    {
        lock (_lock)
        {
            Stop();

            _debounceSeconds = Math.Max(1, debounceSeconds);
            _isEnabled = true;

            var validPaths = sourcePaths
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (validPaths.Count == 0)
            {
                LoggerService.Instance.LogWarning("監視対象の同期元フォルダが指定されていないため、リアルタイム監視を開始できません。", "Watcher");
                return;
            }

            foreach (var path in validPaths)
            {
                AddAndStartWatcher(path);
            }
        }
    }

    private void AddAndStartWatcher(string path)
    {
        var item = new WatcherItem { Path = path };
        _watchers[path] = item;

        if (!Directory.Exists(path))
        {
            LoggerService.Instance.LogWarning($"同期元フォルダ ({path}) が存在しないため、再試行待機を開始します。", "Watcher");
            StartReconnectWatcher(item);
            return;
        }

        try
        {
            var watcher = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName
                             | NotifyFilters.DirectoryName
                             | NotifyFilters.LastWrite
                             | NotifyFilters.Size
                             | NotifyFilters.CreationTime
            };

            watcher.Changed += (s, e) => OnFileSystemEvent(item, e);
            watcher.Created += (s, e) => OnFileSystemEvent(item, e);
            watcher.Deleted += (s, e) => OnFileSystemEvent(item, e);
            watcher.Renamed += (s, e) => OnRenamedEvent(item, e);
            watcher.Error += (s, e) => OnWatcherError(item, e);

            watcher.EnableRaisingEvents = true;
            item.Watcher = watcher;

            LoggerService.Instance.LogInfo($"リアルタイム監視を開始しました: {path} (待機秒数: {_debounceSeconds}秒)", "Watcher");
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"監視の開始に失敗しました ({path}): {ex.Message}", "Watcher");
            StartReconnectWatcher(item);
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _isEnabled = false;
            foreach (var item in _watchers.Values)
            {
                item.Dispose();
            }
            _watchers.Clear();
        }
    }

    private void OnFileSystemEvent(WatcherItem item, FileSystemEventArgs e)
    {
        ResetDebounceTimer(item, e.FullPath, e.ChangeType.ToString());
    }

    private void OnRenamedEvent(WatcherItem item, RenamedEventArgs e)
    {
        ResetDebounceTimer(item, e.FullPath, $"Renamed from {e.OldName}");
    }

    private void ResetDebounceTimer(WatcherItem item, string filePath, string reason)
    {
        lock (_lock)
        {
            if (!_isEnabled) return;

            LoggerService.Instance.LogDebug($"変更検知 [{item.Path}]: {filePath} ({reason}) - デバウンス待機開始 ({_debounceSeconds}秒)", "Watcher");

            item.DebounceTimer?.Dispose();
            item.DebounceTimer = new Timer(_ => OnDebounceTimerElapsed(item), null, TimeSpan.FromSeconds(_debounceSeconds), Timeout.InfiniteTimeSpan);
        }
    }

    private void OnDebounceTimerElapsed(WatcherItem item)
    {
        lock (_lock)
        {
            if (!_isEnabled) return;
            item.DebounceTimer?.Dispose();
            item.DebounceTimer = null;
        }

        LoggerService.Instance.LogInfo($"変更後の待機時間が経過したため、自動同期をトリガーします: {item.Path}", "Watcher");
        try
        {
            ChangeDetectedForPathAndSettled?.Invoke(item.Path);
            ChangeDetectedAndSettled?.Invoke();
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"デバウンス同期呼出例外: {ex.Message}", "Watcher");
        }
    }

    private void OnWatcherError(WatcherItem item, ErrorEventArgs e)
    {
        var ex = e.GetException();
        LoggerService.Instance.LogWarning($"ファイル監視エラーまたは切断を検知しました [{item.Path}]: {ex?.Message}", "Watcher");
        WatcherErrorOccurred?.Invoke(ex?.Message ?? "Unknown error");

        lock (_lock)
        {
            if (_isEnabled)
            {
                item.Dispose();
                StartReconnectWatcher(item);
            }
        }
    }

    private void StartReconnectWatcher(WatcherItem item)
    {
        if (item.IsReconnecting) return;
        item.IsReconnecting = true;

        Task.Run(async () =>
        {
            while (_isEnabled && !_isDisposed)
            {
                await Task.Delay(15000); // 15秒ごとに再接続試行
                if (!_isEnabled || _isDisposed) break;

                if (!string.IsNullOrWhiteSpace(item.Path) && Directory.Exists(item.Path))
                {
                    LoggerService.Instance.LogInfo($"同期元フォルダの再接続を確認しました。監視を再開します: {item.Path}", "Watcher");
                    lock (_lock)
                    {
                        if (_isEnabled && !_isDisposed)
                        {
                            item.IsReconnecting = false;
                            AddAndStartWatcher(item.Path);
                        }
                    }
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
