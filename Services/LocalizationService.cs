using System;
using System.Globalization;
using System.Windows;
using WoodStreamFileSync.Models;

namespace WoodStreamFileSync.Services;

/// <summary>
/// アプリケーションの表示言語（日本語/英語/OS連動）の切り替えおよびリソース文字列の取得を管理するシングルトンサービスクラス
/// </summary>
public class LocalizationService
{
    private static LocalizationService? _instance;

    /// <summary>
    /// <see cref="LocalizationService"/> のシングルトンインスタンス
    /// </summary>
    public static LocalizationService Instance => _instance ??= new LocalizationService();

    private AppLanguage _currentLanguageMode = AppLanguage.System;
    private ResourceDictionary? _currentLanguageDict;

    /// <summary>
    /// 表示言語が切り替わった際に発生するイベント
    /// </summary>
    public event Action? LanguageChanged;

    /// <summary>
    /// 現在選択されている言語モード（システム/日本語/英語）を取得します
    /// </summary>
    public AppLanguage CurrentLanguageMode => _currentLanguageMode;

    /// <summary>
    /// 現在実際に適用されている言語が日本語かどうかを取得します
    /// </summary>
    public bool IsJapanese { get; private set; }

    /// <summary>
    /// 指定された言語モードを適用し、アプリケーションの文字列リソース辞書を更新します
    /// </summary>
    /// <param name="mode">適用する言語モード</param>
    public void ApplyLanguage(AppLanguage mode)
    {
        _currentLanguageMode = mode;
        bool isJa = mode switch
        {
            AppLanguage.Japanese => true,
            AppLanguage.English => false,
            _ => IsWindowsJapanese()
        };

        IsJapanese = isJa;
        UpdateLanguageDictionary(isJa);
        LanguageChanged?.Invoke();
    }

    /// <summary>
    /// 現在の Windows OS の UI カルチャが日本語かどうかを判定します
    /// </summary>
    /// <returns>OSのUI言語が日本語の場合は true、それ以外は false</returns>
    public static bool IsWindowsJapanese()
    {
        try
        {
            var culture = CultureInfo.CurrentUICulture;
            return culture.Name.StartsWith("ja", StringComparison.OrdinalIgnoreCase) ||
                   culture.TwoLetterISOLanguageName.Equals("ja", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// XAML リソース辞書から指定キーに対応するローカライズ文字列を取得します
    /// </summary>
    /// <param name="key">リソースキー</param>
    /// <returns>ローカライズ文字列。見つからない場合はキー自体を返却</returns>
    public string GetString(string key)
    {
        if (Application.Current != null && Application.Current.Resources.Contains(key))
        {
            return Application.Current.Resources[key] as string ?? key;
        }
        return key;
    }

    /// <summary>
    /// WPF アプリケーションリソース内の言語リソース辞書（Strings.ja.xaml / Strings.en.xaml）を動的に差し替えます
    /// </summary>
    /// <param name="isJapanese">日本語辞書を適用する場合は true、英語辞書の場合は false</param>
    private void UpdateLanguageDictionary(bool isJapanese)
    {
        var dictUri = isJapanese
            ? new Uri("Strings/Strings.ja.xaml", UriKind.Relative)
            : new Uri("Strings/Strings.en.xaml", UriKind.Relative);

        var newDict = new ResourceDictionary { Source = dictUri };

        if (Application.Current != null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var dicts = Application.Current.Resources.MergedDictionaries;
                ResourceDictionary? existingDict = null;

                foreach (var d in dicts)
                {
                    if (d.Source != null && (d.Source.ToString().Contains("Strings.ja") || d.Source.ToString().Contains("Strings.en")))
                    {
                        existingDict = d;
                        break;
                    }
                }

                if (existingDict != null)
                {
                    dicts.Remove(existingDict);
                }
                dicts.Add(newDict);
                _currentLanguageDict = newDict;
            });
        }
    }
}
