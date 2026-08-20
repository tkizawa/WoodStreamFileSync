using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using WoodStreamFileSync.Models;
using WoodStreamFileSync.Services;

namespace WoodStreamFileSync.ViewModels;

/// <summary>
/// 設定画面全体のデータバインディング、各種同期パラメータの編集、設定保存・テスト実行を制御する ViewModel
/// </summary>
public class SettingsViewModel : ViewModelBase
{
    private readonly ConfigManager _configManager;
    private readonly SyncManager _syncManager;

    private ObservableCollection<FolderPairViewModel> _folderPairs = new();
    private FolderPairViewModel? _selectedFolderPair;

    private bool _enablePeriodicSync = true;
    private int _periodicIntervalMinutes = 30;
    private bool _enableRealtimeSync = true;
    private int _debounceDelaySeconds = 10;
    private bool _enableNasAuth = false;
    private string _nasUsername = "";
    private string _nasPassword = "";
    private bool _isMirror = true;
    private bool _includeEmptySubdirectories = true;
    private int _retryCount = 1;
    private int _waitTimeSeconds = 1;
    private string _additionalArguments = "";
    private string _excludeFiles = "";
    private string _excludeDirs = "";
    private AppTheme _themeMode = AppTheme.System;
    private AppLanguage _languageMode = AppLanguage.System;
    private bool _launchAtStartup = false;
    private bool _minimizeToTrayOnClose = true;
    private bool _showNotificationOnSuccess = false;
    private bool _showNotificationOnError = true;

    private string _connectionTestResult = "";
    private bool _isConnectionTestSuccess = false;
    private bool _isTestingConnection = false;
    private string _statusMessage = "待機中";
    private SyncStatus _currentSyncStatus = SyncStatus.Idle;

    /// <summary>
    /// 同期フォルダペアのリスト
    /// </summary>
    public ObservableCollection<FolderPairViewModel> FolderPairs
    {
        get => _folderPairs;
        set => SetProperty(ref _folderPairs, value);
    }

    /// <summary>
    /// リスト上で現在選択されているフォルダペア
    /// </summary>
    public FolderPairViewModel? SelectedFolderPair
    {
        get => _selectedFolderPair;
        set => SetProperty(ref _selectedFolderPair, value);
    }

    /// <summary>
    /// 定期同期間隔の選択肢リスト（分単位）
    /// </summary>
    public ObservableCollection<int> IntervalOptions { get; } = new() { 5, 10, 15, 30, 60, 120 };

    /// <summary>
    /// テーマの選択肢リスト
    /// </summary>
    public ObservableCollection<AppTheme> ThemeOptions { get; } = new() { AppTheme.System, AppTheme.Light, AppTheme.Dark };

    /// <summary>
    /// 言語の選択肢リスト
    /// </summary>
    public ObservableCollection<AppLanguage> LanguageOptions { get; } = new() { AppLanguage.System, AppLanguage.Japanese, AppLanguage.English };

    /// <summary>
    /// 選択中のUIテーマ（変更時に即時適用）
    /// </summary>
    public AppTheme ThemeMode
    {
        get => _themeMode;
        set
        {
            if (SetProperty(ref _themeMode, value))
            {
                ThemeService.Instance.ApplyTheme(value);
            }
        }
    }

    /// <summary>
    /// 選択中の表示言語（変更時に即時適用）
    /// </summary>
    public AppLanguage LanguageMode
    {
        get => _languageMode;
        set
        {
            if (SetProperty(ref _languageMode, value))
            {
                LocalizationService.Instance.ApplyLanguage(value);
                UpdateStatusText();
            }
        }
    }

    /// <summary>
    /// 定期同期の有効/無効
    /// </summary>
    public bool EnablePeriodicSync
    {
        get => _enablePeriodicSync;
        set => SetProperty(ref _enablePeriodicSync, value);
    }

    /// <summary>
    /// 定期同期間隔（分単位）
    /// </summary>
    public int PeriodicIntervalMinutes
    {
        get => _periodicIntervalMinutes;
        set => SetProperty(ref _periodicIntervalMinutes, value);
    }

    /// <summary>
    /// リアルタイム監視同期の有効/無効
    /// </summary>
    public bool EnableRealtimeSync
    {
        get => _enableRealtimeSync;
        set => SetProperty(ref _enableRealtimeSync, value);
    }

    /// <summary>
    /// リアルタイム監視のデバウンス待機時間（秒単位）
    /// </summary>
    public int DebounceDelaySeconds
    {
        get => _debounceDelaySeconds;
        set => SetProperty(ref _debounceDelaySeconds, value);
    }

    /// <summary>
    /// NAS事前認証の有効/無効
    /// </summary>
    public bool EnableNasAuth
    {
        get => _enableNasAuth;
        set => SetProperty(ref _enableNasAuth, value);
    }

    /// <summary>
    /// NAS認証用ユーザー名
    /// </summary>
    public string NasUsername
    {
        get => _nasUsername;
        set => SetProperty(ref _nasUsername, value);
    }

    /// <summary>
    /// NAS認証用パスワード
    /// </summary>
    public string NasPassword
    {
        get => _nasPassword;
        set => SetProperty(ref _nasPassword, value);
    }

    /// <summary>
    /// Robocopy /MIR (ミラーリング) 有効フラグ
    /// </summary>
    public bool IsMirror
    {
        get => _isMirror;
        set => SetProperty(ref _isMirror, value);
    }

    /// <summary>
    /// Robocopy /E (空のサブディレクトリを含む) 有効フラグ
    /// </summary>
    public bool IncludeEmptySubdirectories
    {
        get => _includeEmptySubdirectories;
        set => SetProperty(ref _includeEmptySubdirectories, value);
    }

    /// <summary>
    /// Robocopy 再試行回数 (/R:n)
    /// </summary>
    public int RetryCount
    {
        get => _retryCount;
        set => SetProperty(ref _retryCount, value);
    }

    /// <summary>
    /// Robocopy 再試行待機秒数 (/W:n)
    /// </summary>
    public int WaitTimeSeconds
    {
        get => _waitTimeSeconds;
        set => SetProperty(ref _waitTimeSeconds, value);
    }

    /// <summary>
    /// Robocopy 追加引数文字列
    /// </summary>
    public string AdditionalArguments
    {
        get => _additionalArguments;
        set => SetProperty(ref _additionalArguments, value);
    }

    /// <summary>
    /// 除外ファイルパターン文字列 (/XF)
    /// </summary>
    public string ExcludeFiles
    {
        get => _excludeFiles;
        set => SetProperty(ref _excludeFiles, value);
    }

    /// <summary>
    /// 除外ディレクトリパターン文字列 (/XD)
    /// </summary>
    public string ExcludeDirs
    {
        get => _excludeDirs;
        set => SetProperty(ref _excludeDirs, value);
    }

    /// <summary>
    /// Windows スタートアップ自動起動フラグ
    /// </summary>
    public bool LaunchAtStartup
    {
        get => _launchAtStartup;
        set => SetProperty(ref _launchAtStartup, value);
    }

    /// <summary>
    /// 初回免責事項への同意フラグ
    /// </summary>
    public bool HasAcceptedDisclaimer { get; set; } = false;

    /// <summary>
    /// 閉じるボタン押下時にタスクトレイへ最小化するかどうか
    /// </summary>
    public bool MinimizeToTrayOnClose
    {
        get => _minimizeToTrayOnClose;
        set => SetProperty(ref _minimizeToTrayOnClose, value);
    }

    /// <summary>
    /// 同期成功時のトースト通知フラグ
    /// </summary>
    public bool ShowNotificationOnSuccess
    {
        get => _showNotificationOnSuccess;
        set => SetProperty(ref _showNotificationOnSuccess, value);
    }

    /// <summary>
    /// 同期エラー時のトースト通知フラグ
    /// </summary>
    public bool ShowNotificationOnError
    {
        get => _showNotificationOnError;
        set => SetProperty(ref _showNotificationOnError, value);
    }

    /// <summary>
    /// NAS 接続テスト結果メッセージ
    /// </summary>
    public string ConnectionTestResult
    {
        get => _connectionTestResult;
        set => SetProperty(ref _connectionTestResult, value);
    }

    /// <summary>
    /// NAS 接続テストが成功したかどうか
    /// </summary>
    public bool IsConnectionTestSuccess
    {
        get => _isConnectionTestSuccess;
        set => SetProperty(ref _isConnectionTestSuccess, value);
    }

    /// <summary>
    /// NAS 接続テストの非同期実行中フラグ
    /// </summary>
    public bool IsTestingConnection
    {
        get => _isTestingConnection;
        set => SetProperty(ref _isTestingConnection, value);
    }

    /// <summary>
    /// 画面下部に表示する同期ステータスメッセージ
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>
    /// 現在の同期ステータス
    /// </summary>
    public SyncStatus CurrentSyncStatus
    {
        get => _currentSyncStatus;
        set => SetProperty(ref _currentSyncStatus, value);
    }

    /// <summary>
    /// 新規フォルダペア追加コマンド
    /// </summary>
    public ICommand AddFolderPairCommand { get; }

    /// <summary>
    /// フォルダペア削除コマンド
    /// </summary>
    public ICommand RemoveFolderPairCommand { get; }

    /// <summary>
    /// 単一フォルダペアの個別手動同期実行コマンド
    /// </summary>
    public ICommand SyncSinglePairCommand { get; }

    /// <summary>
    /// 同期元フォルダ参照コマンド
    /// </summary>
    public ICommand BrowseSourceCommand { get; }

    /// <summary>
    /// 同期先フォルダ参照コマンド
    /// </summary>
    public ICommand BrowseDestinationCommand { get; }

    /// <summary>
    /// NAS 認証接続テストコマンド
    /// </summary>
    public ICommand TestNasConnectionCommand { get; }

    /// <summary>
    /// 設定保存コマンド
    /// </summary>
    public ICommand SaveSettingsCommand { get; }

    /// <summary>
    /// 今すぐ全ペア同期実行コマンド
    /// </summary>
    public ICommand SyncNowCommand { get; }

    /// <summary>
    /// 設定保存が完了した際に発生するイベント
    /// </summary>
    public event Action? SettingsSaved;

    /// <summary>
    /// <see cref="SettingsViewModel"/> の新しいインスタンスを初期化し、各種コマンドおよび設定を読み込みます
    /// </summary>
    /// <param name="configManager">設定マネージャーインスタンス</param>
    /// <param name="syncManager">同期マネージャーインスタンス</param>
    public SettingsViewModel(ConfigManager configManager, SyncManager syncManager)
    {
        _configManager = configManager;
        _syncManager = syncManager;

        // 同期ステータス変更イベントを購読してUIを更新
        _syncManager.StatusChanged += (_, status) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentSyncStatus = status;
                UpdateStatusText();
            });
        };

        AddFolderPairCommand = new RelayCommand(AddFolderPair);
        RemoveFolderPairCommand = new RelayCommand<FolderPairViewModel>(RemoveFolderPair);
        SyncSinglePairCommand = new AsyncRelayCommand<FolderPairViewModel>(SyncSinglePairAsync);
        BrowseSourceCommand = new RelayCommand(BrowseSource);
        BrowseDestinationCommand = new RelayCommand(BrowseDestination);
        TestNasConnectionCommand = new AsyncRelayCommand(TestNasConnectionAsync);
        SaveSettingsCommand = new RelayCommand(() => SaveSettings(showDialog: true));
        SyncNowCommand = new AsyncRelayCommand(async () =>
        {
            SaveSettings(showDialog: false);
            await _syncManager.ExecuteSyncAsync(LocalizationService.Instance.GetString("Settings.BtnSyncNow"));
        });

        LoadFromConfig();
    }

    /// <summary>
    /// 選択中のフォルダペアに対する同期元選択ダイアログを開きます
    /// </summary>
    private void BrowseSource()
    {
        if (SelectedFolderPair == null) return;
        var loc = LocalizationService.Instance;
        var dialog = new OpenFolderDialog
        {
            Title = loc.GetString("Settings.SourceDialogTitle"),
            InitialDirectory = Directory.Exists(SelectedFolderPair.SourcePath) ? SelectedFolderPair.SourcePath : ""
        };
        if (dialog.ShowDialog() == true)
        {
            SelectedFolderPair.SourcePath = dialog.FolderName;
        }
    }

    /// <summary>
    /// 選択中のフォルダペアに対する同期先選択ダイアログを開きます
    /// </summary>
    private void BrowseDestination()
    {
        if (SelectedFolderPair == null) return;
        var loc = LocalizationService.Instance;
        var dialog = new OpenFolderDialog
        {
            Title = loc.GetString("Settings.DestDialogTitle"),
            InitialDirectory = Directory.Exists(SelectedFolderPair.DestinationPath) ? SelectedFolderPair.DestinationPath : ""
        };
        if (dialog.ShowDialog() == true)
        {
            SelectedFolderPair.DestinationPath = dialog.FolderName;
        }
    }

    /// <summary>
    /// 新規の同期フォルダペアを作成してリストに追加します
    /// </summary>
    private void AddFolderPair()
    {
        var newPair = new FolderPairViewModel
        {
            Name = $"同期設定 {FolderPairs.Count + 1}",
            IsEnabled = true
        };
        FolderPairs.Add(newPair);
        SelectedFolderPair = newPair;
    }

    /// <summary>
    /// 指定された同期フォルダペア（または選択中のペア）をリストから削除します
    /// </summary>
    /// <param name="pair">削除対象のペア（省略時は選択中のペア）</param>
    private void RemoveFolderPair(FolderPairViewModel? pair)
    {
        var target = pair ?? SelectedFolderPair;
        if (target == null) return;

        var loc = LocalizationService.Instance;
        var msg = loc.IsJapanese
            ? $"「{target.DisplayName}」の設定を削除しますか？"
            : $"Are you sure you want to remove '{target.DisplayName}'?";
        var title = loc.IsJapanese ? "削除確認" : "Confirm Delete";

        if (MessageBox.Show(msg, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            FolderPairs.Remove(target);
            if (SelectedFolderPair == target)
            {
                SelectedFolderPair = FolderPairs.FirstOrDefault();
            }
        }
    }

    /// <summary>
    /// 指定された特定のフォルダペアのみを手動で同期実行します
    /// </summary>
    /// <param name="pair">同期対象のペア</param>
    private async Task SyncSinglePairAsync(FolderPairViewModel? pair)
    {
        var target = pair ?? SelectedFolderPair;
        if (target == null) return;

        SaveSettings(showDialog: false);
        await _syncManager.ExecuteSyncAsync($"手動同期 [{target.DisplayName}]", target.ToModel());
    }

    /// <summary>
    /// 現在の同期ステータスおよび言語設定に基づいてステータステキストを更新します
    /// </summary>
    private void UpdateStatusText()
    {
        var loc = LocalizationService.Instance;
        StatusMessage = CurrentSyncStatus switch
        {
            SyncStatus.Idle => loc.GetString("Status.Idle"),
            SyncStatus.Syncing => loc.GetString("Status.Syncing"),
            SyncStatus.Success => $"{loc.GetString("Status.Success")} ({_syncManager.LastSyncTime:HH:mm:ss})",
            SyncStatus.Warning => loc.GetString("Status.Warning"),
            SyncStatus.Error => loc.GetString("Status.Error"),
            _ => "Unknown"
        };
    }

    /// <summary>
    /// 設定ファイルから各プロパティの値を読み込み、ViewModelに反映します
    /// </summary>
    public void LoadFromConfig()
    {
        var config = _configManager.LoadConfig();

        FolderPairs.Clear();
        if (config.FolderPairs != null && config.FolderPairs.Count > 0)
        {
            foreach (var pair in config.FolderPairs)
            {
                FolderPairs.Add(new FolderPairViewModel(pair));
            }
        }

        // 初期状態で何もなければ空のペアを1つ追加
        if (FolderPairs.Count == 0)
        {
            FolderPairs.Add(new FolderPairViewModel
            {
                Name = "メイン同期",
                SourcePath = config.SourcePath,
                DestinationPath = config.DestinationPath,
                IsEnabled = true
            });
        }

        SelectedFolderPair = FolderPairs.FirstOrDefault();

        EnablePeriodicSync = config.EnablePeriodicSync;
        PeriodicIntervalMinutes = config.PeriodicIntervalMinutes;
        EnableRealtimeSync = config.EnableRealtimeSync;
        DebounceDelaySeconds = config.DebounceDelaySeconds;
        EnableNasAuth = config.EnableNasAuth;
        NasUsername = config.NasUsername;
        NasPassword = config.NasPassword;
        IsMirror = config.Robocopy.IsMirror;
        IncludeEmptySubdirectories = config.Robocopy.IncludeEmptySubdirectories;
        RetryCount = config.Robocopy.RetryCount;
        WaitTimeSeconds = config.Robocopy.WaitTimeSeconds;
        AdditionalArguments = config.Robocopy.AdditionalArguments;
        ExcludeFiles = config.Robocopy.ExcludeFiles;
        ExcludeDirs = config.Robocopy.ExcludeDirs;
        ThemeMode = config.ThemeMode;
        LanguageMode = config.LanguageMode;
        HasAcceptedDisclaimer = config.HasAcceptedDisclaimer;
        LaunchAtStartup = config.LaunchAtStartup;
        MinimizeToTrayOnClose = config.MinimizeToTrayOnClose;
        ShowNotificationOnSuccess = config.ShowNotificationOnSuccess;
        ShowNotificationOnError = config.ShowNotificationOnError;

        UpdateStatusText();
    }

    /// <summary>
    /// 現在の ViewModel の各プロパティ値を <see cref="AppConfig"/> モデルにまとめます
    /// </summary>
    /// <returns>設定データモデル</returns>
    public AppConfig ToConfig()
    {
        var firstPair = FolderPairs.FirstOrDefault();
        return new AppConfig
        {
            SourcePath = firstPair?.SourcePath.Trim() ?? "",
            DestinationPath = firstPair?.DestinationPath.Trim() ?? "",
            FolderPairs = FolderPairs.Select(vm => vm.ToModel()).ToList(),
            EnablePeriodicSync = EnablePeriodicSync,
            PeriodicIntervalMinutes = PeriodicIntervalMinutes,
            EnableRealtimeSync = EnableRealtimeSync,
            DebounceDelaySeconds = DebounceDelaySeconds,
            EnableNasAuth = EnableNasAuth,
            NasUsername = NasUsername.Trim(),
            NasPassword = NasPassword,
            Robocopy = new RobocopyOptions
            {
                IsMirror = IsMirror,
                IncludeEmptySubdirectories = IncludeEmptySubdirectories,
                RetryCount = RetryCount,
                WaitTimeSeconds = WaitTimeSeconds,
                AdditionalArguments = AdditionalArguments.Trim(),
                ExcludeFiles = ExcludeFiles.Trim(),
                ExcludeDirs = ExcludeDirs.Trim()
            },
            ThemeMode = ThemeMode,
            LanguageMode = LanguageMode,
            HasAcceptedDisclaimer = HasAcceptedDisclaimer,
            LaunchAtStartup = LaunchAtStartup,
            MinimizeToTrayOnClose = MinimizeToTrayOnClose,
            ShowNotificationOnSuccess = ShowNotificationOnSuccess,
            ShowNotificationOnError = ShowNotificationOnError
        };
    }

    /// <summary>
    /// 設定内容をファイルに保存し、同期マネージャーに即時反映します
    /// </summary>
    /// <param name="showDialog">完了メッセージダイアログを表示するかどうか</param>
    private void SaveSettings(bool showDialog = true)
    {
        var loc = LocalizationService.Instance;
        var config = ToConfig();
        if (_configManager.SaveConfig(config))
        {
            _syncManager.ApplyConfig(config);
            SettingsSaved?.Invoke();
            if (showDialog)
            {
                var msg = loc.IsJapanese ? "設定を保存し、同期エンジンに適用しました。" : "Settings saved and applied successfully.";
                var title = loc.IsJapanese ? "設定保存" : "Saved";
                MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        else
        {
            if (showDialog)
            {
                var msg = loc.IsJapanese ? "設定の保存に失敗しました。" : "Failed to save settings.";
                var title = loc.IsJapanese ? "エラー" : "Error";
                MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// 設定されたUNCパスおよび認証情報を用いて、NAS接続テストを非同期実行します
    /// </summary>
    private async Task TestNasConnectionAsync()
    {
        // 選択中または登録済みのペアからUNCパスを探す
        string target = "";
        if (SelectedFolderPair != null)
        {
            if (NasAuthenticator.IsUncPath(SelectedFolderPair.DestinationPath))
                target = SelectedFolderPair.DestinationPath;
            else if (NasAuthenticator.IsUncPath(SelectedFolderPair.SourcePath))
                target = SelectedFolderPair.SourcePath;
        }

        if (string.IsNullOrWhiteSpace(target))
        {
            var uncPair = FolderPairs.FirstOrDefault(p => NasAuthenticator.IsUncPath(p.DestinationPath) || NasAuthenticator.IsUncPath(p.SourcePath));
            if (uncPair != null)
            {
                target = NasAuthenticator.IsUncPath(uncPair.DestinationPath) ? uncPair.DestinationPath : uncPair.SourcePath;
            }
        }

        if (string.IsNullOrWhiteSpace(target))
        {
            ConnectionTestResult = LocalizationService.Instance.IsJapanese
                ? "UNCパス (\\\\server\\share) が設定されている同期フォルダがありません。"
                : "No folder pair with UNC path (\\\\server\\share) configured.";
            IsConnectionTestSuccess = false;
            return;
        }

        IsTestingConnection = true;
        ConnectionTestResult = LocalizationService.Instance.GetString("Settings.TestingConnection");

        try
        {
            var (success, message) = await NasAuthenticator.TestConnectionAsync(target, NasUsername, NasPassword);
            IsConnectionTestSuccess = success;
            ConnectionTestResult = $"[{target}] {message}";
        }
        finally
        {
            IsTestingConnection = false;
        }
    }
}
