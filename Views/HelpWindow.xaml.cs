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
    /// <summary>
    /// アプリケーション終了などによる明示的なClose要求かどうか（通常はHideでタスクトレイ常駐）
    /// </summary>
    public bool IsExplicitClose { get; set; } = false;

    /// <summary>
    /// <see cref="HelpWindow"/> クラスの新しいインスタンスを初期化します
    /// </summary>
    public HelpWindow()
    {
        InitializeComponent();

        SourceInitialized += (_, _) =>
        {
            ThemeService.Instance.UpdateWindowTitleBar(this, ThemeService.Instance.IsActualDark);
        };
    }

    /// <summary>
    /// ウィンドウの閉じる（×）イベント処理。明示的なCloseでない場合は非表示（Hide）にします
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
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
        Hide();
    }
}
