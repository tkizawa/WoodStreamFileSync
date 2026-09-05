using System;
using System.ComponentModel;
using System.Windows;
using WoodStreamFileSync.Services;

namespace WoodStreamFileSync.Views;

/// <summary>
/// アプリケーションの使い方や免責事項を表示するヘルプウィンドウのコードビハインド
/// </summary>
public partial class HelpWindow : Window
{
    private readonly ConfigManager _configManager;

    /// <summary>
    /// アプリケーション終了などによる明示的なClose要求かどうか（通常はHideでタスクトレイ常駐）
    /// </summary>
    public bool IsExplicitClose { get; set; } = false;

    /// <summary>
    /// <see cref="HelpWindow"/> クラスの新しいインスタンスを初期化します
    /// </summary>
    /// <param name="configManager">設定マネージャーインスタンス（省略時は新規生成）</param>
    public HelpWindow(ConfigManager? configManager = null)
    {
        InitializeComponent();
        _configManager = configManager ?? new ConfigManager();

        SourceInitialized += (_, _) =>
        {
            ThemeService.Instance.UpdateWindowTitleBar(this, ThemeService.Instance.IsActualDark);

            // プロジェクトルール: ウィンドウ位置およびサイズを復元
            var config = _configManager.LoadConfig();
            WindowPlacementHelper.RestorePlacement(this, config.HelpWindowPlacement);
        };
    }

    /// <summary>
    /// 現在のウィンドウ位置・サイズを設定ファイルに保存します
    /// </summary>
    private void SavePlacement()
    {
        var placement = WindowPlacementHelper.CapturePlacement(this);
        _configManager.SaveWindowPlacement("Help", placement);
    }

    /// <summary>
    /// ウィンドウの閉じる（×）イベント処理。位置・サイズを保存し、明示的なCloseでない場合は非表示（Hide）にします
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        // 閉じる/非表示にするタイミングで位置・サイズを保存
        SavePlacement();

        if (!IsExplicitClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    /// <summary>
    /// 「閉じる」ボタン押下時のイベントハンドラ
    /// </summary>
    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        SavePlacement();
        Hide();
    }
}
