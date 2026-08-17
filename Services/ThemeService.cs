using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;
using WoodStreamFileSync.Models;

namespace WoodStreamFileSync.Services;

public class ThemeService
{
    private static ThemeService? _instance;
    public static ThemeService Instance => _instance ??= new ThemeService();

    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private AppTheme _currentThemeMode = AppTheme.System;
    public bool IsActualDark { get; private set; }

    public event Action<bool>? ThemeChanged;

    public ThemeService()
    {
        SystemEvents.UserPreferenceChanged += (s, e) =>
        {
            if (_currentThemeMode == AppTheme.System)
            {
                ApplyTheme(AppTheme.System);
            }
        };
    }

    public void ApplyTheme(AppTheme mode)
    {
        _currentThemeMode = mode;
        bool isDark = mode switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            _ => IsWindowsDarkMode()
        };

        IsActualDark = isDark;
        UpdateMergedDictionary(isDark);

        // 開いているすべてのウィンドウのタイトルバーとスタイルを更新
        if (Application.Current != null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (Window window in Application.Current.Windows)
                {
                    UpdateWindowTitleBar(window, isDark);
                }
            });
        }

        ThemeChanged?.Invoke(isDark);
    }

    public static bool IsWindowsDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            if (key != null)
            {
                var val = key.GetValue("AppsUseLightTheme");
                if (val is int intVal)
                {
                    return intVal == 0;
                }
            }
        }
        catch { }
        return false;
    }

    private void UpdateMergedDictionary(bool isDark)
    {
        var themeDictUri = isDark
            ? new Uri("Themes/DarkTheme.xaml", UriKind.Relative)
            : new Uri("Themes/LightTheme.xaml", UriKind.Relative);

        var newDict = new ResourceDictionary { Source = themeDictUri };

        if (Application.Current != null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var dicts = Application.Current.Resources.MergedDictionaries;
                // 既存のテーマ辞書を探して置換
                ResourceDictionary? existingThemeDict = null;
                foreach (var d in dicts)
                {
                    if (d.Source != null && (d.Source.ToString().Contains("DarkTheme") || d.Source.ToString().Contains("LightTheme")))
                    {
                        existingThemeDict = d;
                        break;
                    }
                }

                if (existingThemeDict != null)
                {
                    dicts.Remove(existingThemeDict);
                }
                dicts.Add(newDict);
            });
        }
    }

    public void UpdateWindowTitleBar(Window window, bool isDark)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            int useDarkMode = isDark ? 1 : 0;
            // Windows 10 20H1 / Windows 11 以降 (20)
            int hr = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
            if (hr != 0)
            {
                // 古いビルド向け (19)
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useDarkMode, sizeof(int));
            }
        }
        catch { }
    }
}
