using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WoodStreamFileSync.Models;

namespace WoodStreamFileSync.Services;

/// <summary>
/// 同期処理の通知（トースト通知等）要求イベント引数
/// </summary>
public class SyncNotificationEventArgs : EventArgs
{
    /// <summary>
    /// 通知のタイトル
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 通知のメッセージ本文
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// エラーまたは警告に関する通知であるかどうか
    /// </summary>
    public bool IsError { get; set; }
}

/// <summary>
/// 定期同期、リアルタイム監視同期、手動同期の実行と状態を統括管理するシングルトンサービスクラス
/// </summary>
public class SyncManager : IDisposable
{
    private static SyncManager? _instance;

    /// <summary>
    /// <see cref="SyncManager"/> のシングルトンインスタンス
    /// </summary>
    public static SyncManager Instance => _instance ??= new SyncManager();

    /// <summary>
    /// 同時同期実行を防ぐセマフォ（排他制御）
    /// </summary>
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    /// <summary>
    /// Robocopy 実行サービス
    /// </summary>
    private readonly RobocopyRunner _robocopyRunner = new();

    /// <summary>
    /// フォルダ変更検知サービス
    /// </summary>
    private readonly FolderWatcherService _folderWatcher = new();

    /// <summary>
    /// 定期同期間隔タイマー
    /// </summary>
    private Timer? _periodicTimer;

    /// <summary>
    /// 現在適用中の設定
    /// </summary>
    private AppConfig _config = new();

    /// <summary>
    /// 現在の同期ステータス
    /// </summary>
    private SyncStatus _currentStatus = SyncStatus.Idle;

    /// <summary>
    /// 最終同期完了日時
    /// </summary>
    private DateTime? _lastSyncTime;

    /// <summary>
    /// 破棄済みフラグ
    /// </summary>
    private bool _isDisposed;

    /// <summary>
    /// 同期ステータスが変化した際に発生するイベント
    /// </summary>
    public event EventHandler<SyncStatus>? StatusChanged;

    /// <summary>
    /// ユーザーへの通知（トースト通知）が要求された際に発生するイベント
    /// </summary>
    public event EventHandler<SyncNotificationEventArgs>? NotificationRequested;

    /// <summary>
    /// 現在の同期実行ステータスを取得します
    /// </summary>
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

    /// <summary>
    /// 最終同期完了日時を取得します
    /// </summary>
    public DateTime? LastSyncTime => _lastSyncTime;

    /// <summary>
    /// リアルタイム監視が現在動作中かどうかを取得します
    /// </summary>
    public bool IsRealtimeWatcherRunning => _folderWatcher.IsWatching;

    /// <summary>
    /// 現在適用されている設定情報を取得します
    /// </summary>
    public AppConfig Config => _config;

    /// <summary>
    /// <see cref="SyncManager"/> クラスの新しいインスタンスを初期化し、イベントハンドラをバインドします
    /// </summary>
    public SyncManager()
    {
        // リアルタイム変更検知時、該当フォルダペアの同期を非同期実行
        _folderWatcher.ChangeDetectedForPathAndSettled += (sourcePath) =>
        {
            _ = ExecuteSyncForSourcePathAsync(sourcePath, "リアルタイム検知");
        };

        // 監視エラー発生時のステータス更新
        _folderWatcher.WatcherErrorOccurred += (msg) =>
        {
            CurrentStatus = SyncStatus.Warning;
        };
    }

    /// <summary>
    /// 設定情報を渡してサービスを初期化します
    /// </summary>
    /// <param name="config">適用する設定情報</param>
    public void Initialize(AppConfig config)
    {
        ApplyConfig(config);
    }

    /// <summary>
    /// 新しい設定情報を適用し、リアルタイム監視と定期タイマーを再起動・再スケジュールします
    /// </summary>
    /// <param name="config">適用する設定情報</param>
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

    /// <summary>
    /// 設定から有効（IsEnabled == true）な同期フォルダペアのリストを取得します
    /// </summary>
    /// <returns>有効なフォルダペアのリスト</returns>
    private List<SyncFolderPair> GetActiveFolderPairs()
    {
        if (_config.FolderPairs != null && _config.FolderPairs.Count > 0)
        {
            return _config.FolderPairs.Where(p => p.IsEnabled).ToList();
        }

        // 後方互換フォールバック（旧設定値が存在する場合）
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

    /// <summary>
    /// 定期タイマー発火時のコールバックハンドラ
    /// </summary>
    private void OnPeriodicTimerElapsed(object? state)
    {
        LoggerService.Instance.LogInfo("定期タイマーによる同期を開始します。", "SyncManager");
        _ = ExecuteSyncAsync("定期タイマー");
    }

    /// <summary>
    /// リアルタイム監視の有効/無効をトグル切り替えします
    /// </summary>
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

    /// <summary>
    /// 変更検知された同期元パスに該当するフォルダペアを特定して同期を実行します
    /// </summary>
    /// <param name="sourcePath">変更が検知された同期元パス</param>
    /// <param name="triggerSource">同期トリガー名</param>
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

    /// <summary>
    /// 同期処理を非同期実行します（排他制御付き）
    /// </summary>
    /// <param name="triggerSource">実行要因（例: "手動実行", "定期タイマー", "リアルタイム検知" 等）</param>
    /// <param name="targetPair">特定のペアのみ同期する場合は指定。nullの場合は有効な全ペアが対象</param>
    /// <returns>Robocopyの最終実行結果。実行されなかった場合は null</returns>
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

            // 排他ロック試行（先行実行中の同期がある場合はスキップ）
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

                // 完了状態の判定とトースト通知
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

    /// <summary>
    /// アンマネージドリソースおよびタイマー等を解放します
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _periodicTimer?.Dispose();
        _folderWatcher.Dispose();
        _syncLock.Dispose();
    }
}
