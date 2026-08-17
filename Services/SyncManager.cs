using System;
using System.IO;
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
        _folderWatcher.ChangeDetectedAndSettled += () =>
        {
            _ = ExecuteSyncAsync("リアルタイム検知");
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

        // リアルタイム監視の再構成
        if (config.EnableRealtimeSync && !string.IsNullOrWhiteSpace(config.SourcePath))
        {
            _folderWatcher.Start(config.SourcePath, config.DebounceDelaySeconds);
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
            _folderWatcher.Start(_config.SourcePath, _config.DebounceDelaySeconds);
            LoggerService.Instance.LogInfo("リアルタイム監視を有効化しました。", "SyncManager");
        }
        else
        {
            _folderWatcher.Stop();
            LoggerService.Instance.LogInfo("リアルタイム監視を無効化しました。", "SyncManager");
        }
    }

    public Task<RobocopyResult?> ExecuteSyncAsync(string triggerSource)
    {
        // 完全にバックグラウンドスレッドで実行し、UIスレッドを一切ブロックしない
        return Task.Run(async () =>
        {
            if (string.IsNullOrWhiteSpace(_config.SourcePath) || string.IsNullOrWhiteSpace(_config.DestinationPath))
            {
                LoggerService.Instance.LogWarning("同期元または同期先のフォルダパスが設定されていません。", "SyncManager");
                return null;
            }

            // 排他ロックの試行 (既に実行中の場合は重複実行を防ぐ)
            if (!await _syncLock.WaitAsync(0))
            {
                LoggerService.Instance.LogWarning($"別の同期処理が実行中のため、[{triggerSource}] による同期リクエストをスキップしました。", "SyncManager");
                return null;
            }

            CurrentStatus = SyncStatus.Syncing;
            LoggerService.Instance.LogInfo($"同期を開始します (トリガー: {triggerSource})", "SyncManager");

            try
            {
                // 1. NAS 事前認証 (必要な場合)
                if (_config.EnableNasAuth)
                {
                    if (NasAuthenticator.IsUncPath(_config.DestinationPath))
                    {
                        LoggerService.Instance.LogInfo($"同期先NASの事前認証を試行します: {_config.DestinationPath}", "SyncManager");
                        var authResult = NasAuthenticator.Authenticate(_config.DestinationPath, _config.NasUsername, _config.NasPassword);
                        if (!authResult.Success)
                        {
                            LoggerService.Instance.LogError($"同期先NASの事前認証に失敗しました: {authResult.Message}", "SyncManager");
                        }
                        else
                        {
                            LoggerService.Instance.LogInfo(authResult.Message, "SyncManager");
                        }
                    }

                    if (NasAuthenticator.IsUncPath(_config.SourcePath))
                    {
                        LoggerService.Instance.LogInfo($"同期元NASの事前認証を試行します: {_config.SourcePath}", "SyncManager");
                        var authResult = NasAuthenticator.Authenticate(_config.SourcePath, _config.NasUsername, _config.NasPassword);
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
                if (!Directory.Exists(_config.SourcePath))
                {
                    var errorMsg = $"同期元フォルダが存在しません: {_config.SourcePath}";
                    LoggerService.Instance.LogError(errorMsg, "SyncManager");
                    CurrentStatus = SyncStatus.Error;
                    NotificationRequested?.Invoke(this, new SyncNotificationEventArgs
                    {
                        Title = "同期エラー",
                        Message = errorMsg,
                        IsError = true
                    });
                    return null;
                }

                // 3. Robocopy 実行
                var result = await _robocopyRunner.RunAsync(
                    _config.SourcePath,
                    _config.DestinationPath,
                    _config.Robocopy);

                _lastSyncTime = DateTime.Now;

                if (result.Success)
                {
                    CurrentStatus = SyncStatus.Success;
                    if (_config.ShowNotificationOnSuccess)
                    {
                        NotificationRequested?.Invoke(this, new SyncNotificationEventArgs
                        {
                            Title = "同期完了",
                            Message = $"フォルダ同期が完了しました ({result.SummaryMessage})",
                            IsError = false
                        });
                    }
                }
                else
                {
                    CurrentStatus = SyncStatus.Error;
                    if (_config.ShowNotificationOnError)
                    {
                        NotificationRequested?.Invoke(this, new SyncNotificationEventArgs
                        {
                            Title = "同期エラー",
                            Message = $"Robocopy でエラーが発生しました: {result.SummaryMessage}",
                            IsError = true
                        });
                    }
                }

                return result;
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
