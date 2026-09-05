using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using WoodStreamFileSync.Services;
using WoodStreamFileSync.ViewModels;

namespace WoodStreamFileSync.Views;

/// <summary>
/// 実行ログをリアルタイムに確認できるログウィンドウのコードビハインド
/// </summary>
public partial class LogWindow : Window
{
    private readonly LogViewModel _viewModel;
    private readonly ConfigManager _configManager;

    /// <summary>
    /// アプリケーション終了等による明示的なClose要求かどうか
    /// </summary>
    public bool IsExplicitClose { get; set; } = false;

    /// <summary>
    /// <see cref="LogWindow"/> クラスの新しいインスタンスを初期化し、自動スクロールハンドラ等を設定します
    /// </summary>
    /// <param name="viewModel">バインドする LogViewModel</param>
    /// <param name="configManager">設定マネージャーインスタンス（省略時は新規生成）</param>
    public LogWindow(LogViewModel viewModel, ConfigManager? configManager = null)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _configManager = configManager ?? new ConfigManager();
        DataContext = _viewModel;

        SourceInitialized += (_, _) =>
        {
            ThemeService.Instance.UpdateWindowTitleBar(this, ThemeService.Instance.IsActualDark);

            // プロジェクトルール: ウィンドウ位置およびサイズを復元
            var config = _configManager.LoadConfig();
            WindowPlacementHelper.RestorePlacement(this, config.LogWindowPlacement);
        };

        // 新規ログ追加時の自動スクロール処理
        ((INotifyCollectionChanged)LogListView.Items).CollectionChanged += (_, _) =>
        {
            if (_viewModel.AutoScroll && LogListView.Items.Count > 0)
            {
                LogListView.ScrollIntoView(LogListView.Items[^1]);
            }
        };
    }

    /// <summary>
    /// 現在のウィンドウ位置・サイズを設定ファイルに保存します
    /// </summary>
    private void SavePlacement()
    {
        var placement = WindowPlacementHelper.CapturePlacement(this);
        _configManager.SaveWindowPlacement("Log", placement);
    }

    /// <summary>
    /// ウィンドウ閉じる（×）イベント処理。位置・サイズを保存し、明示的なCloseでない場合は非表示（Hide）にします
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
