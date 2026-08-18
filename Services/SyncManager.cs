using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WoodStreamFileSync.Models;

namespace WoodStreamFileSync.Services;

public class SyncNotificationEventArgs : EventArgs
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsError { get; set; }
}

public class SyncManager : IDisposable
{
    private static SyncManager? _instance;
    public static SyncManager Instance => _instance ??= new SyncManager();

    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly RobocopyRunner _robocopyRunner = new();
    private readonly FolderWatcherService _folderWatcher = new();
    private Timer? _periodicTimer;
    private AppConfig _config = new();
    private SyncStatus _currentStatus = SyncStatus.Idle;
    private DateTime? _lastSyncTime;
    private bool _isDisposed;

    public event EventHandler<SyncStatus>? StatusChanged;
    public event EventHandler<SyncNotificationEventArgs>? NotificationRequested;

    public SyncStatus CurrentStatus
    {
        get => _currentStatus;
        private set
        {
            if (_currentStatus != value)
            {
                _currentStatus = value;
                StatusChanged?.Invoke(this, _currentStatus);
            }
        }
    }

    public DateTime? LastSyncTime => _lastSyncTime;
    public bool IsRealtimeWatcherRunning => _folderWatcher.IsWatching;
    public AppConfig Config => _config;

    public SyncManager()
    {
        _folderWatcher.ChangeDetectedForPathAndSettled += (sourcePath) =>
        {
            _ = ExecuteSyncForSourcePathAsync(sourcePath, "リアルタイム検知");
        };

        _folderWatcher.WatcherErrorOccurred += (msg) =>
        {
            CurrentStatus = SyncStatus.Warning;
        };
    }

    public void Initialize(AppConfig config)
    {
        ApplyConfig(config);
    }

    public void ApplyConfig(AppConfig config)
    {
        _config = config;

        // 有効な同期元フォルダのリストを取得
        var activePairs = GetActiveFolderPairs();
        var sourcePaths = activePairs.Select(p => p.SourcePath).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();

        // リアルタイム監視の再構成
        if (config.EnableRealtimeSync && sourcePaths.Count > 0)
        {
            _folderWatcher.Start(sourcePaths, config.DebounceDelaySeconds);
        }
        else
        {
            _folderWatcher.Stop();
        }

        // 定期タイマーの再構成
        _periodicTimer?.Dispose();
        _periodicTimer = null;

        if (config.EnablePeriodicSync && config.PeriodicIntervalMinutes > 0)
        {
            var interval = TimeSpan.FromMinutes(config.PeriodicIntervalMinutes);
            _periodicTimer = new Timer(OnPeriodicTimerElapsed, null, interval, interval);
            LoggerService.Instance.LogInfo($"定期同期タイマーを設定しました (同期間隔: {config.PeriodicIntervalMinutes}分)", "SyncManager");
        }
        else
        {
            LoggerService.Instance.LogInfo("定期同期は無効化されています。", "SyncManager");
        }
    }

    private List<SyncFolderPair> GetActiveFolderPairs()
    {
        if (_config.FolderPairs != null && _config.FolderPairs.Count > 0)
        {
            return _config.FolderPairs.Where(p => p.IsEnabled).ToList();
        }

        // 後方互換フォールバック
        if (!string.IsNullOrWhiteSpace(_config.SourcePath) && !string.IsNullOrWhiteSpace(_config.DestinationPath))
        {
            return new List<SyncFolderPair>
            {
                new SyncFolderPair
                {
                    SourcePath = _config.SourcePath,
                    DestinationPath = _config.DestinationPath,
                    IsEnabled = true
                }
            };
        }

        return new List<SyncFolderPair>();
    }

    private void OnPeriodicTimerElapsed(object? state)
    {
        LoggerService.Instance.LogInfo("定期タイマーによる同期を開始します。", "SyncManager");
        _ = ExecuteSyncAsync("定期タイマー");
    }

    public void ToggleRealtimeSync()
    {
        _config.EnableRealtimeSync = !_config.EnableRealtimeSync;
        if (_config.EnableRealtimeSync)
        {
            var activePairs = GetActiveFolderPairs();
            var sourcePaths = activePairs.Select(p => p.SourcePath).Where(p => !string.IsNullOrWhiteSpace(p));
            _folderWatcher.Start(sourcePaths, _config.DebounceDelaySeconds);
            LoggerService.Instance.LogInfo("リアルタイム監視を有効化しました。", "SyncManager");
        }
        else
        {
            _folderWatcher.Stop();
            LoggerService.Instance.LogInfo("リアルタイム監視を無効化しました。", "SyncManager");
        }
    }

    private async Task ExecuteSyncForSourcePathAsync(string sourcePath, string triggerSource)
    {
        var activePairs = GetActiveFolderPairs();
        var matchingPairs = activePairs
            .Where(p => string.Equals(p.SourcePath.TrimEnd('\\', '/'), sourcePath.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matchingPairs.Count == 0)
        {
            await ExecuteSyncAsync(triggerSource);
            return;
        }

        foreach (var pair in matchingPairs)
        {
            await ExecuteSyncAsync($"{triggerSource} [{pair.DisplayName}]", pair);
        }
    }

    public Task<RobocopyResult?> ExecuteSyncAsync(string triggerSource, SyncFolderPair? targetPair = null)
    {
        return Task.Run(async () =>
        {
            var pairsToSync = targetPair != null
                ? new List<SyncFolderPair> { targetPair }
                : GetActiveFolderPairs();

            if (pairsToSync.Count == 0)
            {
                LoggerService.Instance.LogWarning("有効な同期フォルダペアが設定されていません。", "SyncManager");
                return null;
            }

            // 排他ロック試行
            if (!await _syncLock.WaitAsync(0))
            {
                LoggerService.Instance.LogWarning($"別の同期処理が実行中のため、[{triggerSource}] による同期リクエストをスキップしました。", "SyncManager");
                return null;
            }

            CurrentStatus = SyncStatus.Syncing;
            LoggerService.Instance.LogInfo($"同期処理を開始します (対象: {pairsToSync.Count}件, トリガー: {triggerSource})", "SyncManager");

            RobocopyResult? lastResult = null;
            int successCount = 0;
            int errorCount = 0;

            try
            {
                foreach (var pair in pairsToSync)
                {
                    if (string.IsNullOrWhiteSpace(pair.SourcePath) || string.IsNullOrWhiteSpace(pair.DestinationPath))
                    {
                        LoggerService.Instance.LogWarning($"同期パスが未設定のためスキップしました: {pair.DisplayName}", "SyncManager");
                        continue;
                    }

                    LoggerService.Instance.LogInfo($"--- 同期開始: {pair.DisplayName} ({pair.SourcePath} -> {pair.DestinationPath}) ---", "SyncManager");

                    // 1. NAS 事前認証 (必要な場合)
                    if (_config.EnableNasAuth)
                    {
                        if (NasAuthenticator.IsUncPath(pair.DestinationPath))
                        {
                            LoggerService.Instance.LogInfo($"同期先NASの事前認証を試行します: {pair.DestinationPath}", "SyncManager");
                            var authResult = NasAuthenticator.Authenticate(pair.DestinationPath, _config.NasUsername, _config.NasPassword);
                            if (!authResult.Success)
                            {
                                LoggerService.Instance.LogError($"同期先NASの事前認証に失敗しました: {authResult.Message}", "SyncManager");
                            }
                            else
                            {
                                LoggerService.Instance.LogInfo(authResult.Message, "SyncManager");
                            }
                        }

                        if (NasAuthenticator.IsUncPath(pair.SourcePath))
                        {
                            LoggerService.Instance.LogInfo($"同期元NASの事前認証を試行します: {pair.SourcePath}", "SyncManager");
                            var authResult = NasAuthenticator.Authenticate(pair.SourcePath, _config.NasUsername, _config.NasPassword);
                            if (!authResult.Success)
                            {
                                LoggerService.Instance.LogError($"同期元NASの事前認証に失敗しました: {authResult.Message}", "SyncManager");
                            }
                            else
                            {
                                LoggerService.Instance.LogInfo(authResult.Message, "SyncManager");
                            }
                        }
                    }

                    // 2. ディレクトリの事前検証
                    if (!Directory.Exists(pair.SourcePath))
                    {
                        var errorMsg = $"同期元フォルダが存在しません [{pair.DisplayName}]: {pair.SourcePath}";
                        LoggerService.Instance.LogError(errorMsg, "SyncManager");
                        pair.LastSyncStatus = SyncStatus.Error;
                        errorCount++;
                        continue;
                    }

                    // 3. Robocopy 実行
                    var result = await _robocopyRunner.RunAsync(
                        pair.SourcePath,
                        pair.DestinationPath,
                        _config.Robocopy);

                    lastResult = result;
                    pair.LastSyncTime = DateTime.Now;

                    if (result.Success)
                    {
                        pair.LastSyncStatus = SyncStatus.Success;
                        successCount++;
                    }
                    else
                    {
                        pair.LastSyncStatus = SyncStatus.Error;
                        errorCount++;
                    }
                }

                _lastSyncTime = DateTime.Now;

                if (errorCount == 0 && successCount > 0)
                {
                    CurrentStatus = SyncStatus.Success;
                    if (_config.ShowNotificationOnSuccess)
                    {
                        NotificationRequested?.Invoke(this, new SyncNotificationEventArgs
                        {
                            Title = "同期完了",
                            Message = $"{successCount} 件のフォルダ同期が完了しました。",
                            IsError = false
                        });
                    }
                }
                else if (errorCount > 0 && successCount > 0)
                {
                    CurrentStatus = SyncStatus.Warning;
                    if (_config.ShowNotificationOnError)
                    {
                        NotificationRequested?.Invoke(this, new SyncNotificationEventArgs
                        {
                            Title = "同期一部完了・警告",
                            Message = $"成功: {successCount}件, エラー: {errorCount}件 でした。ログを確認してください。",
                            IsError = true
                        });
                    }
                }
                else if (errorCount > 0 && successCount == 0)
                {
                    CurrentStatus = SyncStatus.Error;
                    if (_config.ShowNotificationOnError)
                    {
                        NotificationRequested?.Invoke(this, new SyncNotificationEventArgs
                        {
                            Title = "同期エラー",
                            Message = "すべてのフォルダ同期でエラーが発生しました。",
                            IsError = true
                        });
                    }
                }
                else
                {
                    CurrentStatus = SyncStatus.Idle;
                }

                return lastResult;
            }
            catch (Exception ex)
            {
                CurrentStatus = SyncStatus.Error;
                LoggerService.Instance.LogError($"同期処理中の予期しないエラー: {ex.Message}", "SyncManager");
                NotificationRequested?.Invoke(this, new SyncNotificationEventArgs
                {
                    Title = "同期システムエラー",
                    Message = ex.Message,
                    IsError = true
                });
                return null;
            }
            finally
            {
                _syncLock.Release();
            }
        });
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _periodicTimer?.Dispose();
        _folderWatcher.Dispose();
        _syncLock.Dispose();
    }
}
