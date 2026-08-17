using System.Text.Json.Serialization;

namespace WoodStreamFileSync.Models;

public class RobocopyOptions
{
    public bool IsMirror { get; set; } = true;
    public bool IncludeEmptySubdirectories { get; set; } = true;
    public int RetryCount { get; set; } = 1;
    public int WaitTimeSeconds { get; set; } = 1;
    public string AdditionalArguments { get; set; } = "";
    public string ExcludeFiles { get; set; } = "";
    public string ExcludeDirs { get; set; } = "";
}

public class AppConfig
{
    public string SourcePath { get; set; } = "";
    public string DestinationPath { get; set; } = "";

    // 定期同期設定
    public bool EnablePeriodicSync { get; set; } = true;
    public int PeriodicIntervalMinutes { get; set; } = 30;

    // リアルタイム検知設定
    public bool EnableRealtimeSync { get; set; } = true;
    public int DebounceDelaySeconds { get; set; } = 10;

    // NAS認証設定
    public bool EnableNasAuth { get; set; } = false;
    public string NasUsername { get; set; } = "";
    public string NasPasswordEncrypted { get; set; } = "";

    [JsonIgnore]
    public string NasPassword { get; set; } = "";

    // Robocopy 詳細オプション
    public RobocopyOptions Robocopy { get; set; } = new();

    // アプリケーション動作
    public AppTheme ThemeMode { get; set; } = AppTheme.System;
    public AppLanguage LanguageMode { get; set; } = AppLanguage.System;
    public bool LaunchAtStartup { get; set; } = false;
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public bool ShowNotificationOnSuccess { get; set; } = false;
    public bool ShowNotificationOnError { get; set; } = true;
}
