using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using WoodStreamFileSync.Services;
using WoodStreamFileSync.ViewModels;

namespace WoodStreamFileSync.Views;

public partial class LogWindow : Window
{
    private readonly LogViewModel _viewModel;
    public bool IsExplicitClose { get; set; } = false;

    public LogWindow(LogViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        SourceInitialized += (_, _) =>
        {
            ThemeService.Instance.UpdateWindowTitleBar(this, ThemeService.Instance.IsActualDark);
        };

        ((INotifyCollectionChanged)LogListView.Items).CollectionChanged += (_, _) =>
        {
            if (_viewModel.AutoScroll && LogListView.Items.Count > 0)
            {
                LogListView.ScrollIntoView(LogListView.Items[^1]);
            }
        };
    }

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

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Hide();
    }
}
