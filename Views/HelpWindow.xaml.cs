using System;
using System.ComponentModel;
using System.Windows;
using WoodStreamFileSync.Services;

namespace WoodStreamFileSync.Views;

public partial class HelpWindow : Window
{
    public bool IsExplicitClose { get; set; } = false;

    public HelpWindow()
    {
        InitializeComponent();

        SourceInitialized += (_, _) =>
        {
            ThemeService.Instance.UpdateWindowTitleBar(this, ThemeService.Instance.IsActualDark);
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
