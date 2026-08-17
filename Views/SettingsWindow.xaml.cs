using System;
using System.ComponentModel;
using System.Windows;
using WoodStreamFileSync.Services;
using WoodStreamFileSync.ViewModels;

namespace WoodStreamFileSync.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;
    private readonly Action _openLogAction;
    private readonly Action _openHelpAction;
    public bool IsExplicitClose { get; set; } = false;

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

    private void OnOpenLogClicked(object sender, RoutedEventArgs e)
    {
        _openLogAction();
    }

    private void OnOpenHelpClicked(object sender, RoutedEventArgs e)
    {
        _openHelpAction();
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Hide();
    }
}
