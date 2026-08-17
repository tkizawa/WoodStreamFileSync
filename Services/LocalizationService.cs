using System;
using System.Globalization;
using System.Windows;
using WoodStreamFileSync.Models;

namespace WoodStreamFileSync.Services;

public class LocalizationService
{
    private static LocalizationService? _instance;
    public static LocalizationService Instance => _instance ??= new LocalizationService();

    private AppLanguage _currentLanguageMode = AppLanguage.System;
    private ResourceDictionary? _currentLanguageDict;

    public event Action? LanguageChanged;

    public AppLanguage CurrentLanguageMode => _currentLanguageMode;
    public bool IsJapanese { get; private set; }

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

    public string GetString(string key)
    {
        if (Application.Current != null && Application.Current.Resources.Contains(key))
        {
            return Application.Current.Resources[key] as string ?? key;
        }
        return key;
    }

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
