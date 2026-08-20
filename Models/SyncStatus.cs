namespace WoodStreamFileSync.Models;

/// <summary>
/// 同期処理の実行ステータスを表す列挙型
/// </summary>
public enum SyncStatus
{
    /// <summary>
    /// アイドル状態（待機中）
    /// </summary>
    Idle,

    /// <summary>
    /// 同期処理実行中
    /// </summary>
    Syncing,

    /// <summary>
    /// 同期成功（全ファイル正常コピー完了）
    /// </summary>
    Success,

    /// <summary>
    /// 警告終了（一部スキップなど、軽微な問題あり）
    /// </summary>
    Warning,

    /// <summary>
    /// 同期失敗・エラー発生
    /// </summary>
    Error
}
