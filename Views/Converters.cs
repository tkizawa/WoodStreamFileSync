using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using WoodStreamFileSync.Models;

namespace WoodStreamFileSync.Views;

/// <summary>
/// bool 値を <see cref="Visibility"/>（Visible / Collapsed）に変換するコンバーター
/// </summary>
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

/// <summary>
/// bool 値を反転して <see cref="Visibility"/>（Collapsed / Visible）に変換するコンバーター
/// </summary>
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

/// <summary>
/// オブジェクトが null の場合に Visible、非 null の場合に Collapsed を返すコンバーター
/// </summary>
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

/// <summary>
/// オブジェクトが非 null の場合に Visible、null の場合に Collapsed を返すコンバーター
/// </summary>
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

/// <summary>
/// <see cref="LogLevel"/> を対応する表示用カラーブラシ（SolidColorBrush）に変換するコンバーター
/// </summary>
public class LogLevelToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is LogLevel level)
        {
            return level switch
            {
                LogLevel.Info => new SolidColorBrush(Color.FromRgb(40, 116, 240)),     // 青
                LogLevel.Success => new SolidColorBrush(Color.FromRgb(46, 125, 50)),   // 緑
                LogLevel.Warning => new SolidColorBrush(Color.FromRgb(230, 124, 115)), // オレンジ/琥珀
                LogLevel.Error => new SolidColorBrush(Color.FromRgb(211, 47, 47)),     // 赤
                LogLevel.Debug => new SolidColorBrush(Color.FromRgb(117, 117, 117)),   // グレー
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

/// <summary>
/// <see cref="SyncStatus"/> を対応するステータスカラーブラシに変換するコンバーター
/// </summary>
public class SyncStatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SyncStatus status)
        {
            return status switch
            {
                SyncStatus.Idle => new SolidColorBrush(Color.FromRgb(46, 125, 50)),     // 緑（待機中）
                SyncStatus.Syncing => new SolidColorBrush(Color.FromRgb(255, 152, 0)),  // オレンジ（同期中）
                SyncStatus.Success => new SolidColorBrush(Color.FromRgb(46, 125, 50)),  // 緑（成功）
                SyncStatus.Warning => new SolidColorBrush(Color.FromRgb(255, 193, 7)),  // 黄色（警告）
                SyncStatus.Error => new SolidColorBrush(Color.FromRgb(211, 47, 47)),    // 赤（エラー）
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

/// <summary>
/// <see cref="AppTheme"/> 列挙型を表示用文字列に変換するコンバーター
/// </summary>
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

/// <summary>
/// <see cref="AppLanguage"/> 列挙型を表示用文字列に変換するコンバーター
/// </summary>
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
/// WPF の <see cref="PasswordBox"/> に対する双方向データバインディングを可能にする添付プロパティヘルパークラス
/// </summary>
public static class PasswordBoxHelper
{
    /// <summary>
    /// バインド対象のパスワード文字列を保持する添付プロパティ
    /// </summary>
    public static readonly DependencyProperty BoundPassword =
        DependencyProperty.RegisterAttached("BoundPassword", typeof(string), typeof(PasswordBoxHelper),
            new FrameworkPropertyMetadata(string.Empty, OnBoundPasswordChanged));

    /// <summary>
    /// パスワードバインディングを有効化するかどうかを指定する添付プロパティ
    /// </summary>
    public static readonly DependencyProperty BindPassword =
        DependencyProperty.RegisterAttached("BindPassword", typeof(bool), typeof(PasswordBoxHelper),
            new FrameworkPropertyMetadata(false, OnBindPasswordChanged));

    /// <summary>
    /// 循環更新防止用フラグを保持する内部添付プロパティ
    /// </summary>
    private static readonly DependencyProperty UpdatingPassword =
        DependencyProperty.RegisterAttached("UpdatingPassword", typeof(bool), typeof(PasswordBoxHelper),
            new FrameworkPropertyMetadata(false));

    public static string GetBoundPassword(DependencyObject dp) => (string)dp.GetValue(BoundPassword);
    public static void SetBoundPassword(DependencyObject dp, string value) => dp.SetValue(BoundPassword, value);

    public static bool GetBindPassword(DependencyObject dp) => (bool)dp.GetValue(BindPassword);
    public static void SetBindPassword(DependencyObject dp, bool value) => dp.SetValue(BindPassword, value);

    private static bool GetUpdatingPassword(DependencyObject dp) => (bool)dp.GetValue(UpdatingPassword);
    private static void SetUpdatingPassword(DependencyObject dp, bool value) => dp.SetValue(UpdatingPassword, value);

    /// <summary>
    /// ViewModel側のパスワード値変更時に PasswordBox へ反映します
    /// </summary>
    private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PasswordBox box)
        {
            if (GetUpdatingPassword(box)) return;
            box.Password = (string)e.NewValue ?? string.Empty;
        }
    }

    /// <summary>
    /// BindPassword プロパティ設定時に PasswordChanged イベントハンドラを接続/切断します
    /// </summary>
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

    /// <summary>
    /// PasswordBox の入力変更イベントをキャッチし、ViewModel側のバインドプロパティを更新します
    /// </summary>
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
