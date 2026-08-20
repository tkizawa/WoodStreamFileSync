namespace WoodStreamFileSync.Models;

/// <summary>
/// アプリケーションのUIテーマ（外観モード）を表す列挙型
/// </summary>
public enum AppTheme
{
    /// <summary>
    /// OSのテーマ設定（ダーク/ライト）に従う
    /// </summary>
    System,

    /// <summary>
    /// ライトテーマ（明るい配色）
    /// </summary>
    Light,

    /// <summary>
    /// ダークテーマ（暗い配色）
    /// </summary>
    Dark
}
