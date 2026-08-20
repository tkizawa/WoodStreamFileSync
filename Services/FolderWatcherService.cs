using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WoodStreamFileSync.Services;

/// <summary>
/// <see cref="FileSystemWatcher"/> を使用して複数の同期元フォルダの変更をリアルタイム監視し、
/// デバウンス待機後に同期処理イベントを発行するサービスクラス
/// </summary>
public class FolderWatcherService : IDisposable
{
    /// <summary>
    /// 各監視パスごとの FileSystemWatcher とデバウンスタイマー、再接続状態を管理する内部クラス
    /// </summary>
    private class WatcherItem : IDisposable
    {
        /// <summary>
        /// 監視対象のフォルダパス
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// ファイルシステム監視オブジェクト
        /// </summary>
        public FileSystemWatcher? Watcher { get; set; }

        /// <summary>
        /// 変更検知後のデバウンス用タイマー
        /// </summary>
        public Timer? DebounceTimer { get; set; }

        /// <summary>
        /// ネットワーク切断等による再接続待機中フラグ
        /// </summary>
        public bool IsReconnecting { get; set; }

        /// <summary>
        /// タイマーと Watcher のリソースを解放します
        /// </summary>
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

    /// <summary>
    /// パスをキーとした監視アイテムのディクショナリ（大文字小文字を区別しない）
    /// </summary>
    private readonly Dictionary<string, WatcherItem> _watchers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// スレッド同期用ロックオブジェクト
    /// </summary>
    private readonly object _lock = new();

    /// <summary>
    /// 破棄済みフラグ
    /// </summary>
    private bool _isDisposed;

    /// <summary>
    /// デバウンス待機時間（秒）
    /// </summary>
    private int _debounceSeconds = 10;

    /// <summary>
    /// 監視が有効化されているかどうか
    /// </summary>
    private bool _isEnabled;

    /// <summary>
    /// いずれかのフォルダで変更が検知されデバウンス待機が完了した際に発生するイベント
    /// </summary>
    public event Action? ChangeDetectedAndSettled;

    /// <summary>
    /// 特定のフォルダで変更が検知されデバウンス待機が完了した際に発生するイベント（引数は同期元パス）
    /// </summary>
    public event Action<string>? ChangeDetectedForPathAndSettled;

    /// <summary>
    /// フォルダ監視中にエラーや切断が発生した際に発生するイベント
    /// </summary>
    public event Action<string>? WatcherErrorOccurred;

    /// <summary>
    /// 現在いずれかのフォルダでアクティブに監視中であるかを取得します
    /// </summary>
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

    /// <summary>
    /// 単一フォルダの監視を開始します
    /// </summary>
    /// <param name="sourcePath">監視対象の同期元フォルダパス</param>
    /// <param name="debounceSeconds">変更検知後のデバウンス秒数</param>
    public void Start(string sourcePath, int debounceSeconds)
    {
        var paths = string.IsNullOrWhiteSpace(sourcePath) ? Enumerable.Empty<string>() : new[] { sourcePath };
        Start(paths, debounceSeconds);
    }

    /// <summary>
    /// 複数のフォルダパスの監視を一括で開始します
    /// </summary>
    /// <param name="sourcePaths">監視対象のフォルダパスコレクション</param>
    /// <param name="debounceSeconds">変更検知後のデバウンス秒数（最低1秒）</param>
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

    /// <summary>
    /// 指定されたパスに対する WatcherItem を生成し、フォルダが存在すれば監視を開始、存在しなければ再接続待機を開始します
    /// </summary>
    /// <param name="path">監視対象フォルダパス</param>
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

            // 各種変更イベントのハンドラ登録
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

    /// <summary>
    /// すべてのフォルダ監視を停止し、リソースを解放します
    /// </summary>
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

    /// <summary>
    /// ファイルの作成・更新・削除イベントを処理し、デバウンスタイマーをリセットします
    /// </summary>
    private void OnFileSystemEvent(WatcherItem item, FileSystemEventArgs e)
    {
        ResetDebounceTimer(item, e.FullPath, e.ChangeType.ToString());
    }

    /// <summary>
    /// ファイルまたはディレクトリのリネームイベントを処理し、デバウンスタイマーをリセットします
    /// </summary>
    private void OnRenamedEvent(WatcherItem item, RenamedEventArgs e)
    {
        ResetDebounceTimer(item, e.FullPath, $"Renamed from {e.OldName}");
    }

    /// <summary>
    /// デバウンスタイマーをリセットし、指定秒数後に同期トリガーを実行するようスケジュールします
    /// </summary>
    /// <param name="item">対象の監視アイテム</param>
    /// <param name="filePath">変更検知されたファイルパス</param>
    /// <param name="reason">変更理由（作成、変更、リネーム等）</param>
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

    /// <summary>
    /// デバウンス待機時間が経過した際に呼び出され、同期実行イベントを発行します
    /// </summary>
    /// <param name="item">待機が完了した監視アイテム</param>
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

    /// <summary>
    /// 監視エラーやネットワーク切断検知時の処理。エラーイベントを発行し、再接続待機を開始します
    /// </summary>
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

    /// <summary>
    /// ネットワーク切断やフォルダ消失時に、定期的にフォルダの存在を確認して監視を自動再開するタスクを開始します
    /// </summary>
    /// <param name="item">再接続を試行する監視アイテム</param>
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

    /// <summary>
    /// アンマネージドリソースおよびマネージドリソースを解放します
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        Stop();
    }
}
