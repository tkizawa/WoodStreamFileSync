using System;
using System.IO;
using System.Text.Json.Serialization;

namespace WoodStreamFileSync.Models;

/// <summary>
/// 同期対象となる同期元フォルダ・同期先フォルダのペアを表すデータモデル
/// </summary>
public class SyncFolderPair
{
    /// <summary>
    /// フォルダペアを一意に識別するID（UUID文字列）
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// ユーザーが設定した表示名・ラベル（省略時は同期元フォルダ名を使用）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 同期元のフォルダパス (ローカルまたはUNCパス)
    /// </summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>
    /// 同期先のフォルダパス (ローカルまたはUNCパス)
    /// </summary>
    public string DestinationPath { get; set; } = string.Empty;

    /// <summary>
    /// このフォルダペアの同期を有効にするかどうか
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 最終同期完了日時 (実行時のみメモリ保持)
    /// </summary>
    [JsonIgnore]
    public DateTime? LastSyncTime { get; set; }

    /// <summary>
    /// 最終同期実行時のステータス (実行時のみメモリ保持)
    /// </summary>
    [JsonIgnore]
    public SyncStatus LastSyncStatus { get; set; } = SyncStatus.Idle;

    /// <summary>
    /// UI表示用のラベル（Nameが空の場合はSourcePathのフォルダ名またはパス全体から解決）
    /// </summary>
    [JsonIgnore]
    public string DisplayName
    {
        get
        {
            // カスタム名称が設定されている場合はそれを最優先
            if (!string.IsNullOrWhiteSpace(Name))
                return Name;

            // 同期元パスが設定されている場合、ディレクトリ名を抽出
            if (!string.IsNullOrWhiteSpace(SourcePath))
            {
                try
                {
                    var dirName = Path.GetFileName(SourcePath.TrimEnd('\\', '/'));
                    if (!string.IsNullOrEmpty(dirName))
                        return dirName;
                }
                catch { }
                return SourcePath;
            }

            return "新規フォルダペア";
        }
    }

    /// <summary>
    /// 現在のインスタンスの複製を作成します
    /// </summary>
    /// <returns>複製された <see cref="SyncFolderPair"/> オブジェクト</returns>
    public SyncFolderPair Clone()
    {
        return new SyncFolderPair
        {
            Id = this.Id,
            Name = this.Name,
            SourcePath = this.SourcePath,
            DestinationPath = this.DestinationPath,
            IsEnabled = this.IsEnabled,
            LastSyncTime = this.LastSyncTime,
            LastSyncStatus = this.LastSyncStatus
        };
    }
}
