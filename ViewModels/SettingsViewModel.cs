using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using WoodStreamFileSync.Models;
using WoodStreamFileSync.Services;

namespace WoodStreamFileSync.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly ConfigManager _configManager;
    private readonly SyncManager _syncManager;

    private string _sourcePath = "";
    private string _destinationPath = "";
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

    public ObservableCollection<int> IntervalOptions { get; } = new() { 5, 10, 15, 30, 60, 120 };
    public ObservableCollection<AppTheme> ThemeOptions { get; } = new() { AppTheme.System, AppTheme.Light, AppTheme.Dark };
    public ObservableCollection<AppLanguage> LanguageOptions { get; } = new() { AppLanguage.System, AppLanguage.Japanese, AppLanguage.English };

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

    public string SourcePath
    {
        get => _sourcePath;
        set => SetProperty(ref _sourcePath, value);
    }

    public string DestinationPath
    {
        get => _destinationPath;
        set => SetProperty(ref _destinationPath, value);
    }

    public bool EnablePeriodicSync
    {
        get => _enablePeriodicSync;
        set => SetProperty(ref _enablePeriodicSync, value);
    }

    public int PeriodicIntervalMinutes
    {
        get => _periodicIntervalMinutes;
        set => SetProperty(ref _periodicIntervalMinutes, value);
    }

    public bool EnableRealtimeSync
    {
        get => _enableRealtimeSync;
        set => SetProperty(ref _enableRealtimeSync, value);
    }

    public int DebounceDelaySeconds
    {
        get => _debounceDelaySeconds;
        set => SetProperty(ref _debounceDelaySeconds, value);
    }

    public bool EnableNasAuth
    {
        get => _enableNasAuth;
        set => SetProperty(ref _enableNasAuth, value);
    }

    public string NasUsername
    {
        get => _nasUsername;
        set => SetProperty(ref _nasUsername, value);
    }

    public string NasPassword
    {
        get => _nasPassword;
        set => SetProperty(ref _nasPassword, value);
    }

    public bool IsMirror
    {
        get => _isMirror;
        set => SetProperty(ref _isMirror, value);
    }

    public bool IncludeEmptySubdirectories
    {
        get => _includeEmptySubdirectories;
        set => SetProperty(ref _includeEmptySubdirectories, value);
    }

    public int RetryCount
    {
        get => _retryCount;
        set => SetProperty(ref _retryCount, value);
    }

    public int WaitTimeSeconds
    {
        get => _waitTimeSeconds;
        set => SetProperty(ref _waitTimeSeconds, value);
    }

    public string AdditionalArguments
    {
        get => _additionalArguments;
        set => SetProperty(ref _additionalArguments, value);
    }

    public string ExcludeFiles
    {
        get => _excludeFiles;
        set => SetProperty(ref _excludeFiles, value);
    }

    public string ExcludeDirs
    {
        get => _excludeDirs;
        set => SetProperty(ref _excludeDirs, value);
    }

    public bool LaunchAtStartup
    {
        get => _launchAtStartup;
        set => SetProperty(ref _launchAtStartup, value);
    }

    public bool MinimizeToTrayOnClose
    {
        get => _minimizeToTrayOnClose;
        set => SetProperty(ref _minimizeToTrayOnClose, value);
    }

    public bool ShowNotificationOnSuccess
    {
        get => _showNotificationOnSuccess;
        set => SetProperty(ref _showNotificationOnSuccess, value);
    }

    public bool ShowNotificationOnError
    {
        get => _showNotificationOnError;
        set => SetProperty(ref _showNotificationOnError, value);
    }

    public string ConnectionTestResult
    {
        get => _connectionTestResult;
        set => SetProperty(ref _connectionTestResult, value);
    }

    public bool IsConnectionTestSuccess
    {
        get => _isConnectionTestSuccess;
        set => SetProperty(ref _isConnectionTestSuccess, value);
    }

    public bool IsTestingConnection
    {
        get => _isTestingConnection;
        set => SetProperty(ref _isTestingConnection, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public SyncStatus CurrentSyncStatus
    {
        get => _currentSyncStatus;
        set => SetProperty(ref _currentSyncStatus, value);
    }

    public ICommand BrowseSourceCommand { get; }
    public ICommand BrowseDestinationCommand { get; }
    public ICommand TestNasConnectionCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand SyncNowCommand { get; }

    public event Action? SettingsSaved;

    public SettingsViewModel(ConfigManager configManager, SyncManager syncManager)
    {
        _configManager = configManager;
        _syncManager = syncManager;

        _syncManager.StatusChanged += (_, status) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentSyncStatus = status;
                UpdateStatusText();
            });
        };

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

    public void LoadFromConfig()
    {
        var config = _configManager.LoadConfig();

        SourcePath = config.SourcePath;
        DestinationPath = config.DestinationPath;
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
        LaunchAtStartup = config.LaunchAtStartup;
        MinimizeToTrayOnClose = config.MinimizeToTrayOnClose;
        ShowNotificationOnSuccess = config.ShowNotificationOnSuccess;
        ShowNotificationOnError = config.ShowNotificationOnError;

        UpdateStatusText();
    }

    public AppConfig ToConfig()
    {
        return new AppConfig
        {
            SourcePath = SourcePath.Trim(),
            DestinationPath = DestinationPath.Trim(),
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
            LaunchAtStartup = LaunchAtStartup,
            MinimizeToTrayOnClose = MinimizeToTrayOnClose,
            ShowNotificationOnSuccess = ShowNotificationOnSuccess,
            ShowNotificationOnError = ShowNotificationOnError
        };
    }

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

    private void BrowseSource()
    {
        var loc = LocalizationService.Instance;
        var dialog = new OpenFolderDialog
        {
            Title = loc.GetString("Settings.SourceDialogTitle"),
            InitialDirectory = Directory.Exists(SourcePath) ? SourcePath : ""
        };
        if (dialog.ShowDialog() == true)
        {
            SourcePath = dialog.FolderName;
        }
    }

    private void BrowseDestination()
    {
        var loc = LocalizationService.Instance;
        var dialog = new OpenFolderDialog
        {
            Title = loc.GetString("Settings.DestDialogTitle"),
            InitialDirectory = Directory.Exists(DestinationPath) ? DestinationPath : ""
        };
        if (dialog.ShowDialog() == true)
        {
            DestinationPath = dialog.FolderName;
        }
    }

    private async Task TestNasConnectionAsync()
    {
        var target = !string.IsNullOrWhiteSpace(DestinationPath) ? DestinationPath : SourcePath;
        if (string.IsNullOrWhiteSpace(target))
        {
            ConnectionTestResult = "同期先または同期元のパスを入力してください。";
            IsConnectionTestSuccess = false;
            return;
        }

        IsTestingConnection = true;
        ConnectionTestResult = "接続テスト中...";

        try
        {
            var (success, message) = await NasAuthenticator.TestConnectionAsync(target, NasUsername, NasPassword);
            IsConnectionTestSuccess = success;
            ConnectionTestResult = message;
        }
        finally
        {
            IsTestingConnection = false;
        }
    }
}
