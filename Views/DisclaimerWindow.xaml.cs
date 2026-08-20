using System;
using System.Windows;
using WoodStreamFileSync.Services;

namespace WoodStreamFileSync.Views;

/// <summary>
/// 初回起動時に表示される免責事項確認ウィンドウのコードビハインド
/// </summary>
public partial class DisclaimerWindow : Window
{
    /// <summary>
    /// ユーザーが免責事項に同意したかどうか
    /// </summary>
    public bool IsAccepted { get; private set; } = false;

    /// <summary>
    /// <see cref="DisclaimerWindow"/> クラスの新しいインスタンスを初期化します
    /// </summary>
    public DisclaimerWindow()
    {
        InitializeComponent();

        SourceInitialized += (_, _) =>
        {
            ThemeService.Instance.UpdateWindowTitleBar(this, ThemeService.Instance.IsActualDark);
        };
    }

    /// <summary>
    /// 「同意する」ボタン押下時のイベントハンドラ
    /// </summary>
    private void OnAcceptClicked(object sender, RoutedEventArgs e)
    {
        IsAccepted = true;
        DialogResult = true;
        Close();
    }

    /// <summary>
    /// 「同意しない（終了）」ボタン押下時のイベントハンドラ
    /// </summary>
    private void OnDeclineClicked(object sender, RoutedEventArgs e)
    {
        IsAccepted = false;
        DialogResult = false;
        Close();
    }
}
