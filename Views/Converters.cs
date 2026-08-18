using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using WoodStreamFileSync.Models;

namespace WoodStreamFileSync.Views;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is Visibility v && v == Visibility.Visible;
    }
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is Visibility v && v != Visibility.Visible;
    }
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value == null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class NotNullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value != null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class LogLevelToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is LogLevel level)
        {
            return level switch
            {
                LogLevel.Info => new SolidColorBrush(Color.FromRgb(40, 116, 240)),     // Blue
                LogLevel.Success => new SolidColorBrush(Color.FromRgb(46, 125, 50)),   // Green
                LogLevel.Warning => new SolidColorBrush(Color.FromRgb(230, 124, 115)), // Orange/Amber
                LogLevel.Error => new SolidColorBrush(Color.FromRgb(211, 47, 47)),     // Red
                LogLevel.Debug => new SolidColorBrush(Color.FromRgb(117, 117, 117)),   // Gray
                _ => new SolidColorBrush(Colors.Black)
            };
        }
        return new SolidColorBrush(Colors.Black);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class SyncStatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SyncStatus status)
        {
            return status switch
            {
                SyncStatus.Idle => new SolidColorBrush(Color.FromRgb(46, 125, 50)),     // Green
                SyncStatus.Syncing => new SolidColorBrush(Color.FromRgb(255, 152, 0)),  // Orange
                SyncStatus.Success => new SolidColorBrush(Color.FromRgb(46, 125, 50)),  // Green
                SyncStatus.Warning => new SolidColorBrush(Color.FromRgb(255, 193, 7)),  // Yellow/Amber
                SyncStatus.Error => new SolidColorBrush(Color.FromRgb(211, 47, 47)),    // Red
                _ => new SolidColorBrush(Colors.Gray)
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class AppThemeToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is AppTheme theme)
        {
            return theme switch
            {
                AppTheme.System => "Windowsのテーマに追従 (System)",
                AppTheme.Light => "ライトモード (Light)",
                AppTheme.Dark => "ダークモード (Dark)",
                _ => value.ToString() ?? ""
            };
        }
        return value?.ToString() ?? "";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class AppLanguageToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is AppLanguage lang)
        {
            return lang switch
            {
                AppLanguage.System => "Windowsの言語に追従 (System)",
                AppLanguage.Japanese => "日本語 (Japanese)",
                AppLanguage.English => "English (英語)",
                _ => value.ToString() ?? ""
            };
        }
        return value?.ToString() ?? "";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// WPF PasswordBoxのバインディングヘルパー
/// </summary>
public static class PasswordBoxHelper
{
    public static readonly DependencyProperty BoundPassword =
        DependencyProperty.RegisterAttached("BoundPassword", typeof(string), typeof(PasswordBoxHelper),
            new FrameworkPropertyMetadata(string.Empty, OnBoundPasswordChanged));

    public static readonly DependencyProperty BindPassword =
        DependencyProperty.RegisterAttached("BindPassword", typeof(bool), typeof(PasswordBoxHelper),
            new FrameworkPropertyMetadata(false, OnBindPasswordChanged));

    private static readonly DependencyProperty UpdatingPassword =
        DependencyProperty.RegisterAttached("UpdatingPassword", typeof(bool), typeof(PasswordBoxHelper),
            new FrameworkPropertyMetadata(false));

    public static string GetBoundPassword(DependencyObject dp) => (string)dp.GetValue(BoundPassword);
    public static void SetBoundPassword(DependencyObject dp, string value) => dp.SetValue(BoundPassword, value);

    public static bool GetBindPassword(DependencyObject dp) => (bool)dp.GetValue(BindPassword);
    public static void SetBindPassword(DependencyObject dp, bool value) => dp.SetValue(BindPassword, value);

    private static bool GetUpdatingPassword(DependencyObject dp) => (bool)dp.GetValue(UpdatingPassword);
    private static void SetUpdatingPassword(DependencyObject dp, bool value) => dp.SetValue(UpdatingPassword, value);

    private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PasswordBox box)
        {
            if (GetUpdatingPassword(box)) return;
            box.Password = (string)e.NewValue ?? string.Empty;
        }
    }

    private static void OnBindPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PasswordBox box)
        {
            var wasBound = (bool)e.OldValue;
            var needToBind = (bool)e.NewValue;

            if (wasBound)
            {
                box.PasswordChanged -= HandlePasswordChanged;
            }
            if (needToBind)
            {
                box.PasswordChanged += HandlePasswordChanged;
            }
        }
    }

    private static void HandlePasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox box)
        {
            SetUpdatingPassword(box, true);
            SetBoundPassword(box, box.Password);
            SetUpdatingPassword(box, false);
        }
    }
}
