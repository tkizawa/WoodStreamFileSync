using System.Text.Json.Serialization;

namespace WoodStreamFileSync.Models;

/// <summary>
/// Robocopy 実行時の詳細オプションを保持するクラス
/// </summary>
public class RobocopyOptions
{
    /// <summary>
    /// /MIR (ミラーリング) を有効にするかどうか。同期元で削除されたファイルは同期先でも削除されます
    /// </summary>
    public bool IsMirror { get; set; } = true;

    /// <summary>
    /// /E (空のサブディレクトリを含む全サブディレクトリをコピー) を有効にするかどうか
    /// </summary>
    public bool IncludeEmptySubdirectories { get; set; } = true;

    /// <summary>
    /// /R:n (失敗時の再試行回数)
    /// </summary>
    public int RetryCount { get; set; } = 1;

    /// <summary>
    /// /W:n (再試行間の待機秒数)
    /// </summary>
    public int WaitTimeSeconds { get; set; } = 1;

    /// <summary>
    /// ユーザーが指定する任意の追加引数
    /// </summary>
    public string AdditionalArguments { get; set; } = "";

    /// <summary>
    /// /XF (除外するファイル名・パターンのスペース区切り文字列)
    /// </summary>
    public string ExcludeFiles { get; set; } = "";

    /// <summary>
    /// /XD (除外するディレクトリ名・パターンのスペース区切り文字列)
    /// </summary>
    public string ExcludeDirs { get; set; } = "";
}

/// <summary>
/// アプリケーション全体の設定を保持するデータモデル
/// </summary>
public class AppConfig
{
    // 単一フォルダ設定 (旧バージョンからの後方互換用)
    /// <summary>
    /// 単一同期元フォルダパス (後方互換用)
    /// </summary>
    public string SourcePath { get; set; } = "";

    /// <summary>
    /// 単一同期先フォルダパス (後方互換用)
    /// </summary>
    public string DestinationPath { get; set; } = "";

    // 複数同期フォルダペア設定
    /// <summary>
    /// 同期対象のフォルダペアリスト
    /// </summary>
    public List<SyncFolderPair> FolderPairs { get; set; } = new();

    // 定期同期設定
    /// <summary>
    /// 定期実行による同期を有効にするかどうか
    /// </summary>
    public bool EnablePeriodicSync { get; set; } = true;

    /// <summary>
    /// 定期同期の実行間隔 (分単位)
    /// </summary>
    public int PeriodicIntervalMinutes { get; set; } = 30;

    // リアルタイム検知設定
    /// <summary>
    /// ファイル変更検知によるリアルタイム同期を有効にするかどうか
    /// </summary>
    public bool EnableRealtimeSync { get; set; } = true;

    /// <summary>
    /// 変更検知後のデバウンス待機時間 (秒単位)
    /// </summary>
    public int DebounceDelaySeconds { get; set; } = 10;

    // NAS認証設定
    /// <summary>
    /// NAS / 共有フォルダへの認証接続を有効にするかどうか
    /// </summary>
    public bool EnableNasAuth { get; set; } = false;

    /// <summary>
    /// NAS 認証用のユーザー名
    /// </summary>
    public string NasUsername { get; set; } = "";

    /// <summary>
    /// DPAPIで暗号化された NAS 認証用パスワード
    /// </summary>
    public string NasPasswordEncrypted { get; set; } = "";

    /// <summary>
    /// メモリ上でのみ扱う平文パスワード (JSONシリアライズ対象外)
    /// </summary>
    [JsonIgnore]
    public string NasPassword { get; set; } = "";

    // Robocopy 詳細オプション
    /// <summary>
    /// Robocopy 実行オプション
    /// </summary>
    public RobocopyOptions Robocopy { get; set; } = new();

    // アプリケーション動作
    /// <summary>
    /// 表示テーマ (システム / ライト / ダーク)
    /// </summary>
    public AppTheme ThemeMode { get; set; } = AppTheme.System;

    /// <summary>
    /// 表示言語 (システム / 日本語 / 英語)
    /// </summary>
    public AppLanguage LanguageMode { get; set; } = AppLanguage.System;

    /// <summary>
    /// 初回起動時の免責事項に同意済みかどうか
    /// </summary>
    public bool HasAcceptedDisclaimer { get; set; } = false;

    /// <summary>
    /// Windows 起動時に自動起動するかどうか
    /// </summary>
    public bool LaunchAtStartup { get; set; } = false;

    /// <summary>
    /// ウィンドウの「×」ボタン押下時にタスクトレイに最小化するかどうか
    /// </summary>
    public bool MinimizeToTrayOnClose { get; set; } = true;

    /// <summary>
    /// 同期成功時にトースト通知を表示するかどうか
    /// </summary>
    public bool ShowNotificationOnSuccess { get; set; } = false;

    /// <summary>
    /// 同期エラー発生時にトースト通知を表示するかどうか
    /// </summary>
    public bool ShowNotificationOnError { get; set; } = true;
}
