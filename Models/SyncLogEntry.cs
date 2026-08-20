using System;

namespace WoodStreamFileSync.Models;

/// <summary>
/// ログの重大度・種別を表す列挙型
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// 一般的な情報ログ
    /// </summary>
    Info,

    /// <summary>
    /// 同期成功や処理完了を表すログ
    /// </summary>
    Success,

    /// <summary>
    /// 警告ログ（ファイルスキップや一時的な通信失敗など）
    /// </summary>
    Warning,

    /// <summary>
    /// エラーログ（同期失敗や例外発生など）
    /// </summary>
    Error,

    /// <summary>
    /// 詳細なデバッグ用ログ
    /// </summary>
    Debug
}

/// <summary>
/// アプリケーションログの1レコードを表すデータモデル
/// </summary>
public class SyncLogEntry
{
    /// <summary>
    /// ログ発生日時
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// ログレベル（重大度）
    /// </summary>
    public LogLevel Level { get; set; } = LogLevel.Info;

    /// <summary>
    /// ログメッセージ本文
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// ログの発生元（例: "SyncManager", "Robocopy", "Watcher" 等）
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// "yyyy-MM-dd HH:mm:ss" 形式でフォーマットされた日時文字列
    /// </summary>
    public string FormattedTimestamp => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>
    /// ログファイル出力およびテキスト表示用の文字列を生成します
    /// </summary>
    public override string ToString()
    {
        var prefix = Source != null ? $"[{Source}] " : "";
        return $"[{FormattedTimestamp}] [{Level.ToString().ToUpperInvariant()}] {prefix}{Message}";
    }
}
