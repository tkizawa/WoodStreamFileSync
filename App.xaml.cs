using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using WoodStreamFileSync.Models;
using WoodStreamFileSync.Services;
using WoodStreamFileSync.ViewModels;
using WoodStreamFileSync.Views;

namespace WoodStreamFileSync;

public partial class App : Application
{
    private static Mutex? _mutex;
    private const string AppMutexName = "Global\\WoodStreamFileSync_SingleInstance_Mutex";

    private TaskbarIcon? _trayIcon;
    private ConfigManager _configManager = null!;
    private SyncManager _syncManager = null!;
    private LoggerService _logger = null!;

    private SettingsViewModel _settingsViewModel = null!;
    private LogViewModel _logViewModel = null!;
    private SettingsWindow? _settingsWindow;
    private LogWindow? _logWindow;
    private HelpWindow? _helpWindow;

    private MenuItem? _realtimeMenuItem;

    protected override void OnStartup(StartupEventArgs e)
    {
        // 1. 二重起動防止 (Mutex)
        _mutex = new Mutex(true, AppMutexName, out bool isNewInstance);
        if (!isNewInstance)
        {
            MessageBox.Show("WoodStreamFileSync は既に実行中です。タスクトレイを確認してください。", "多重起動防止", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        // 2. サービス初期化
        _logger = LoggerService.Instance;
        _logger.LogInfo("アプリケーションを開始しました。", "App");

        _configManager = new ConfigManager();
        var config = _configManager.LoadConfig();

        // テーマ & 言語の初期化 (システム準拠 / ライト / ダーク, 日本語 / 英語)
        ThemeService.Instance.ApplyTheme(config.ThemeMode);
        LocalizationService.Instance.ApplyLanguage(config.LanguageMode);
        LocalizationService.Instance.LanguageChanged += () =>
        {
            Dispatcher.Invoke(() => SetupTrayIcon());
        };

        // 初回起動時の免責事項同意チェック
        if (!config.HasAcceptedDisclaimer)
        {
            var disclaimerWindow = new DisclaimerWindow();
            var accepted = disclaimerWindow.ShowDialog();
            if (accepted != true || !disclaimerWindow.IsAccepted)
            {
                _logger.LogWarning("免責事項に同意されなかったため、アプリケーションを終了します。", "App");
                _mutex?.ReleaseMutex();
                _mutex?.Dispose();
                Shutdown();
                return;
            }

            config.HasAcceptedDisclaimer = true;
            _configManager.SaveConfig(config);
            _logger.LogInfo("利用上の注意事項・免責事項に同意しました。", "App");
        }

        _syncManager = SyncManager.Instance;
        _syncManager.Initialize(config);

        // 3. ViewModels初期化
        _settingsViewModel = new SettingsViewModel(_configManager, _syncManager);
        _logViewModel = new LogViewModel(_logger);

        // 4. トレイアイコンの作成
        SetupTrayIcon();

        // 5. 同期イベント購読
        _syncManager.StatusChanged += OnSyncStatusChanged;
        _syncManager.NotificationRequested += OnNotificationRequested;

        // 初回起動でパスが未設定の場合は設定画面を表示
        bool hasConfiguredFolders = config.FolderPairs?.Any(p => !string.IsNullOrWhiteSpace(p.SourcePath) && !string.IsNullOrWhiteSpace(p.DestinationPath)) == true
                                    || (!string.IsNullOrWhiteSpace(config.SourcePath) && !string.IsNullOrWhiteSpace(config.DestinationPath));

        if (!hasConfiguredFolders)
        {
            ShowSettingsWindow();
        }
        else
        {
            _trayIcon?.ShowNotification("WoodStreamFileSync 常駐開始", "バックグラウンドでフォルダ同期を監視しています。", NotificationIcon.Info);
        }
    }

    private void SetupTrayIcon()
    {
        var loc = LocalizationService.Instance;

        if (_trayIcon == null)
        {
            _trayIcon = new TaskbarIcon
            {
                ToolTipText = $"{loc.GetString("App.Title")} ({loc.GetString("Status.Idle")})",
                Icon = GetAppIcon()
            };
            _trayIcon.TrayMouseDoubleClick += (_, _) => ShowSettingsWindow();
        }
        else
        {
            _trayIcon.ToolTipText = $"{loc.GetString("App.Title")} ({loc.GetString("Status.Idle")})";
        }

        // コンテキストメニュー作成
        var contextMenu = new ContextMenu();

        var syncNowItem = new MenuItem
        {
            Header = loc.GetString("Tray.SyncNow"),
            FontWeight = FontWeights.Bold
        };
        syncNowItem.Click += async (_, _) =>
        {
            await _syncManager.ExecuteSyncAsync("タスクトレイ手動実行");
        };
        contextMenu.Items.Add(syncNowItem);

        _realtimeMenuItem = new MenuItem
        {
            Header = loc.GetString("Tray.RealtimeSync"),
            IsChecked = _syncManager.Config.EnableRealtimeSync
        };
        _realtimeMenuItem.Click += (_, _) =>
        {
            _syncManager.ToggleRealtimeSync();
            _realtimeMenuItem.IsChecked = _syncManager.Config.EnableRealtimeSync;
        };
        contextMenu.Items.Add(_realtimeMenuItem);

        contextMenu.Items.Add(new Separator());

        var settingsItem = new MenuItem { Header = loc.GetString("Tray.Settings") };
        settingsItem.Click += (_, _) => ShowSettingsWindow();
        contextMenu.Items.Add(settingsItem);

        var logItem = new MenuItem { Header = loc.GetString("Tray.Logs") };
        logItem.Click += (_, _) => ShowLogWindow();
        contextMenu.Items.Add(logItem);

        var helpItem = new MenuItem { Header = loc.GetString("Tray.Help") };
        helpItem.Click += (_, _) => ShowHelpWindow();
        contextMenu.Items.Add(helpItem);

        contextMenu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = loc.GetString("Tray.Exit") };
        exitItem.Click += (_, _) => ExitApplication();
        contextMenu.Items.Add(exitItem);

        _trayIcon.ContextMenu = contextMenu;
        _trayIcon.ForceCreate();
    }

    private System.Drawing.Icon GetAppIcon()
    {
        try
        {
            var iconUri = new Uri("pack://application:,,,/Resources/app_icon.ico");
            var streamInfo = GetResourceStream(iconUri);
            if (streamInfo != null)
            {
                return new System.Drawing.Icon(streamInfo.Stream);
            }
        }
        catch { }

        return System.Drawing.SystemIcons.Application;
    }

    private void OnSyncStatusChanged(object? sender, SyncStatus status)
    {
        Dispatcher.Invoke(() =>
        {
            if (_trayIcon == null) return;

            var statusStr = status switch
            {
                SyncStatus.Idle => "待機中",
                SyncStatus.Syncing => "同期中...",
                SyncStatus.Success => "同期完了 (最新)",
                SyncStatus.Warning => "警告",
                SyncStatus.Error => "エラー発生",
                _ => ""
            };

            _trayIcon.ToolTipText = $"WoodStreamFileSync - {statusStr}";
        });
    }

    private void OnNotificationRequested(object? sender, SyncNotificationEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (_trayIcon == null) return;
            var icon = e.IsError ? NotificationIcon.Error : NotificationIcon.Info;
            _trayIcon.ShowNotification(e.Title, e.Message, icon);
        });
    }

    public void ShowSettingsWindow()
    {
        if (_settingsWindow == null)
        {
            _settingsWindow = new SettingsWindow(_settingsViewModel, ShowLogWindow, ShowHelpWindow);
        }

        _settingsViewModel.LoadFromConfig();
        _settingsWindow.Show();
        _settingsWindow.WindowState = WindowState.Normal;
        _settingsWindow.Activate();
    }

    public void ShowLogWindow()
    {
        if (_logWindow == null)
        {
            _logWindow = new LogWindow(_logViewModel);
        }

        _logWindow.Show();
        _logWindow.WindowState = WindowState.Normal;
        _logWindow.Activate();
    }

    public void ShowHelpWindow()
    {
        if (_helpWindow == null)
        {
            _helpWindow = new HelpWindow();
        }

        _helpWindow.Show();
        _helpWindow.WindowState = WindowState.Normal;
        _helpWindow.Activate();
    }

    private void ExitApplication()
    {
        _logger.LogInfo("アプリケーションを終了します。", "App");

        if (_settingsWindow != null)
        {
            _settingsWindow.IsExplicitClose = true;
            _settingsWindow.Close();
        }

        if (_logWindow != null)
        {
            _logWindow.IsExplicitClose = true;
            _logWindow.Close();
        }

        if (_helpWindow != null)
        {
            _helpWindow.IsExplicitClose = true;
            _helpWindow.Close();
        }

        _syncManager.Dispose();

        if (_trayIcon != null)
        {
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        _mutex?.ReleaseMutex();
        _mutex?.Dispose();

        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
