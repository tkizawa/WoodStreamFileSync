using System;
using System.Windows;
using WoodStreamFileSync.Services;

namespace WoodStreamFileSync.Views;

public partial class DisclaimerWindow : Window
{
    public bool IsAccepted { get; private set; } = false;

    public DisclaimerWindow()
    {
        InitializeComponent();

        SourceInitialized += (_, _) =>
        {
            ThemeService.Instance.UpdateWindowTitleBar(this, ThemeService.Instance.IsActualDark);
        };
    }

    private void OnAcceptClicked(object sender, RoutedEventArgs e)
    {
        IsAccepted = true;
        DialogResult = true;
        Close();
    }

    private void OnDeclineClicked(object sender, RoutedEventArgs e)
    {
        IsAccepted = false;
        DialogResult = false;
        Close();
    }
}
