using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using WoodStreamFileSync.Models;

namespace WoodStreamFileSync.Services;

public class ConfigManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _configFilePath;
    private const string StartupRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupAppName = "WoodStreamFileSync";

    public ConfigManager()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "WoodStreamFileSync");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        _configFilePath = Path.Combine(dir, "config.json");
    }

    public string ConfigFilePath => _configFilePath;

    public AppConfig LoadConfig()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var json = File.ReadAllText(_configFilePath, Encoding.UTF8);
                var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                if (config != null)
                {
                    // DPAPIでパスワードを復号
                    if (!string.IsNullOrEmpty(config.NasPasswordEncrypted))
                    {
                        config.NasPassword = DecryptPassword(config.NasPasswordEncrypted);
                    }
                    return config;
                }
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"設定の読み込みに失敗しました: {ex.Message}", "ConfigManager");
        }

        return new AppConfig();
    }

    public bool SaveConfig(AppConfig config)
    {
        try
        {
            // パスワードをDPAPIで暗号化
            if (!string.IsNullOrEmpty(config.NasPassword))
            {
                config.NasPasswordEncrypted = EncryptPassword(config.NasPassword);
            }
            else
            {
                config.NasPasswordEncrypted = "";
            }

            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(_configFilePath, json, Encoding.UTF8);

            // スタートアップ設定の更新
            UpdateStartupRegistration(config.LaunchAtStartup);

            LoggerService.Instance.LogInfo("設定を保存しました。", "ConfigManager");
            return true;
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"設定の保存に失敗しました: {ex.Message}", "ConfigManager");
            return false;
        }
    }

    public static string EncryptPassword(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return "";
        try
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedBytes);
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"パスワード暗号化エラー: {ex.Message}", "DPAPI");
            return "";
        }
    }

    public static string DecryptPassword(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return "";
        try
        {
            var cipherBytes = Convert.FromBase64String(cipherText);
            var plainBytes = ProtectedData.Unprotect(cipherBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"パスワード復号エラー: {ex.Message}", "DPAPI");
            return "";
        }
    }

    private void UpdateStartupRegistration(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true);
            if (key == null) return;

            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath)) return;

            if (enable)
            {
                key.SetValue(StartupAppName, $"\"{exePath}\"");
            }
            else
            {
                if (key.GetValue(StartupAppName) != null)
                {
                    key.DeleteValue(StartupAppName, false);
                }
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogWarning($"スタートアップ登録の更新に失敗しました: {ex.Message}", "Startup");
        }
    }
}
