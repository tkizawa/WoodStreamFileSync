using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Win32;
using WoodStreamFileSync.Models;

namespace WoodStreamFileSync.Services;

/// <summary>
/// アプリケーション設定ファイルの読み込み、保存、暗号化、スタートアップ登録を管理するサービスクラス
/// </summary>
public class ConfigManager
{
    /// <summary>
    /// 設定ファイルのJSONシリアライズ・デシリアライズ用オプション
    /// （日本語文字列のエスケープ防止、インデント整形、大文字小文字無視を適用）
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// 設定ファイルのフルパス
    /// </summary>
    private readonly string _configFilePath;

    /// <summary>
    /// Windows スタートアップ登録用のレジストリキーパス
    /// </summary>
    private const string StartupRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>
    /// スタートアップレジストリ登録時のアプリケーション名
    /// </summary>
    private const string StartupAppName = "WoodStreamFileSync";

    /// <summary>
    /// <see cref="ConfigManager"/> クラスの新しいインスタンスを初期化します。
    /// 保存先ディレクトリ（%LocalAppData%\WoodStreamFileSync）の確認・作成および旧Roaming設定のマイグレーションを行います。
    /// </summary>
    public ConfigManager()
    {
        // PC固有の設定として管理するため AppData\Local を使用
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(localAppData, "WoodStreamFileSync");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        _configFilePath = Path.Combine(dir, "config.json");

        // 以前の Roaming 領域に設定が存在する場合、自動移行
        MigrateFromRoamingIfNeeded(dir);
    }

    /// <summary>
    /// 現在使用中の設定ファイルパスを取得します
    /// </summary>
    public string ConfigFilePath => _configFilePath;

    /// <summary>
    /// 以前のバージョンで Roaming フォルダに作成されていた設定ファイルが存在する場合、Local フォルダへコピー移行します
    /// </summary>
    /// <param name="localDir">Localフォルダのパス</param>
    private void MigrateFromRoamingIfNeeded(string localDir)
    {
        try
        {
            if (!File.Exists(_configFilePath))
            {
                var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var oldConfig = Path.Combine(roamingAppData, "WoodStreamFileSync", "config.json");
                if (File.Exists(oldConfig))
                {
                    File.Copy(oldConfig, _configFilePath, true);
                    LoggerService.Instance.LogInfo("以前の設定ファイル (Roaming) を Local 領域へ移行しました。", "ConfigManager");
                }
            }
        }
        catch
        {
            // 移行失敗時は握りつぶす
        }
    }

    /// <summary>
    /// 設定ファイルから設定情報を読み込みます。ファイルが存在しない・破損している場合はデフォルト設定を返します
    /// </summary>
    /// <returns>読み込まれた <see cref="AppConfig"/> オブジェクト</returns>
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
                    // 後方互換性マイグレーション: FolderPairsが空で旧単一フォルダ設定が存在する場合に移行
                    if ((config.FolderPairs == null || config.FolderPairs.Count == 0) &&
                        (!string.IsNullOrWhiteSpace(config.SourcePath) || !string.IsNullOrWhiteSpace(config.DestinationPath)))
                    {
                        config.FolderPairs = new List<SyncFolderPair>
                        {
                            new SyncFolderPair
                            {
                                SourcePath = config.SourcePath,
                                DestinationPath = config.DestinationPath,
                                IsEnabled = true
                            }
                        };
                    }

                    // DPAPIで暗号化されたパスワードを復号
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

    /// <summary>
    /// 指定された設定情報をJSONファイルとして保存し、必要に応じてWindowsスタートアップ登録を更新します
    /// </summary>
    /// <param name="config">保存する設定情報</param>
    /// <returns>保存に成功した場合は true、失敗した場合は false</returns>
    public bool SaveConfig(AppConfig config)
    {
        try
        {
            // パスワードをWindows DPAPIで暗号化
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

    /// <summary>
    /// 各GUIウィンドウの終了時配置情報（位置・サイズ）を保存します。
    /// プロジェクトルール: 「終了時のウィンドウ位置およびサイズを保存し、次回起動時に復元すること」
    /// </summary>
    /// <param name="windowType">ウィンドウ種別 ("Settings", "Log", "Help")</param>
    /// <param name="placement">保存する配置情報</param>
    public void SaveWindowPlacement(string windowType, WindowPlacementConfig placement)
    {
        try
        {
            var config = LoadConfig();
            switch (windowType)
            {
                case "Settings":
                    config.SettingsWindowPlacement = placement;
                    break;
                case "Log":
                    config.LogWindowPlacement = placement;
                    break;
                case "Help":
                    config.HelpWindowPlacement = placement;
                    break;
            }
            SaveConfig(config);
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"ウィンドウ配置情報の保存に失敗しました ({windowType}): {ex.Message}", "ConfigManager");
        }
    }

    /// <summary>
    /// Windows DPAPI (ProtectedData) を使用して平文文字列を暗号化し、Base64文字列で返します
    /// </summary>
    /// <param name="plainText">暗号化する平文文字列</param>
    /// <returns>Base64エンコードされた暗号化文字列</returns>
    public static string EncryptPassword(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return "";
        try
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            // 現在のログオンユーザーコンテキストで暗号化
            var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedBytes);
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"パスワード暗号化エラー: {ex.Message}", "DPAPI");
            return "";
        }
    }

    /// <summary>
    /// Windows DPAPI (ProtectedData) で暗号化されたBase64文字列を復号して平文文字列を返します
    /// </summary>
    /// <param name="cipherText">Base64エンコードされた暗号化文字列</param>
    /// <returns>復号された平文文字列</returns>
    public static string DecryptPassword(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return "";
        try
        {
            var cipherBytes = Convert.FromBase64String(cipherText);
            // 現在のログオンユーザーコンテキストで復号
            var plainBytes = ProtectedData.Unprotect(cipherBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"パスワード復号エラー: {ex.Message}", "DPAPI");
            return "";
        }
    }

    /// <summary>
    /// Windows 起動時の自動起動（スタートアップ）レジストリ登録を更新します
    /// </summary>
    /// <param name="enable">有効にする場合は true、解除する場合は false</param>
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
                // 実行ファイルのフルパスをクォートで囲んで登録
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
