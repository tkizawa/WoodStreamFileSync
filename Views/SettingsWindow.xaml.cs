using System;
using System.ComponentModel;
using System.Windows;
using WoodStreamFileSync.Services;
using WoodStreamFileSync.ViewModels;

namespace WoodStreamFileSync.Views;

/// <summary>
/// アプリケーションの詳細設定（同期元・先、スケジュール、NAS認証、テーマなど）を行う設定ウィンドウのコードビハインド
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;
    private readonly Action _openLogAction;
    private readonly Action _openHelpAction;

    /// <summary>
    /// アプリケーション終了等による明示的なClose要求かどうか
    /// </summary>
    public bool IsExplicitClose { get; set; } = false;

    /// <summary>
    /// <see cref="SettingsWindow"/> クラスの新しいインスタンスを初期化します
    /// </summary>
    /// <param name="viewModel">バインドする SettingsViewModel</param>
    /// <param name="openLogAction">ログ画面を開くコールバックアクション</param>
    /// <param name="openHelpAction">ヘルプ画面を開くコールバックアクション</param>
    public SettingsWindow(SettingsViewModel viewModel, Action openLogAction, Action openHelpAction)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _openLogAction = openLogAction;
        _openHelpAction = openHelpAction;
        DataContext = _viewModel;

        SourceInitialized += (_, _) =>
        {
            ThemeService.Instance.UpdateWindowTitleBar(this, ThemeService.Instance.IsActualDark);
        };
    }

    /// <summary>
    /// ウィンドウ閉じる（×）イベント処理。タスクトレイ最小化設定時は非表示（Hide）にします
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!IsExplicitClose && _viewModel.MinimizeToTrayOnClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    /// <summary>
    /// 「ログ表示」ボタン押下時のイベントハンドラ
    /// </summary>
    private void OnOpenLogClicked(object sender, RoutedEventArgs e)
    {
        _openLogAction();
    }

    /// <summary>
    /// 「ヘルプ」ボタン押下時のイベントハンドラ
    /// </summary>
    private void OnOpenHelpClicked(object sender, RoutedEventArgs e)
    {
        _openHelpAction();
    }

    /// <summary>
    /// 「閉じる」ボタン押下時のイベントハンドラ
    /// </summary>
    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Hide();
    }
}
