using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;
using WoodStreamFileSync.Models;

namespace WoodStreamFileSync.Services;

/// <summary>
/// アプリケーション全体のUIテーマ（ライト/ダーク/OS連動）の切り替えおよび
/// Windows DWM によるタイトルバーのダークモード適用を制御するサービスクラス
/// </summary>
public class ThemeService
{
    private static ThemeService? _instance;

    /// <summary>
    /// <see cref="ThemeService"/> のシングルトンインスタンス
    /// </summary>
    public static ThemeService Instance => _instance ??= new ThemeService();

    /// <summary>
    /// Windows テーマ設定（Personalize）レジストリキーパス
    /// </summary>
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>
    /// Windows 10 20H1 (Build 19041) 以降および Windows 11 用の DWM タイトルバー ダークモード属性定数
    /// </summary>
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    /// <summary>
    /// Windows 10 1809〜1909 (Build 17763〜18363) 用の DWM タイトルバー ダークモード属性定数 (旧定義)
    /// </summary>
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;

    /// <summary>
    /// ウィンドウ属性を設定する Desktop Window Manager (DWM) API
    /// </summary>
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private AppTheme _currentThemeMode = AppTheme.System;

    /// <summary>
    /// 現在実際に適用されているテーマがダークテーマかどうかを取得します
    /// </summary>
    public bool IsActualDark { get; private set; }

    /// <summary>
    /// テーマが変更された際に発生するイベント（引数はダークモードフラグ）
    /// </summary>
    public event Action<bool>? ThemeChanged;

    /// <summary>
    /// <see cref="ThemeService"/> クラスの新しいインスタンスを初期化し、OSのユーザー設定変更イベントを監視します
    /// </summary>
    public ThemeService()
    {
        // OS側のテーマ（ライト/ダーク）切り替えをリアルタイム検知
        SystemEvents.UserPreferenceChanged += (s, e) =>
        {
            if (_currentThemeMode == AppTheme.System)
            {
                ApplyTheme(AppTheme.System);
            }
        };
    }

    /// <summary>
    /// 指定されたテーマモードを適用し、リソースディクショナリおよび開いているウィンドウのタイトルバーを更新します
    /// </summary>
    /// <param name="mode">適用するテーマモード（システム/ライト/ダーク）</param>
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

    /// <summary>
    /// Windows OS のアプリ設定がダークモードに設定されているかをレジストリから判定します
    /// </summary>
    /// <returns>OSがダークモードの場合は true、ライトモードの場合は false</returns>
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
                    return intVal == 0; // 0 = Dark, 1 = Light
                }
            }
        }
        catch { }
        return false;
    }

    /// <summary>
    /// WPF アプリケーションリソース内のテーマリソース辞書（DarkTheme.xaml / LightTheme.xaml）を差し替えます
    /// </summary>
    /// <param name="isDark">ダークテーマにするかどうか</param>
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

    /// <summary>
    /// DWM API を呼び出して、指定された WPF ウィンドウのネイティブタイトルバーのダークモード色を同期設定します
    /// </summary>
    /// <param name="window">対象のウィンドウ</param>
    /// <param name="isDark">ダークモードにするかどうか</param>
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
                // 古いビルド向けフォールバック (19)
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useDarkMode, sizeof(int));
            }
        }
        catch { }
    }
}
