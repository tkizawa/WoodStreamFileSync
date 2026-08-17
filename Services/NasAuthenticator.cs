using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WoodStreamFileSync.Services;

public static class NasAuthenticator
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NETRESOURCE
    {
        public int dwScope;
        public int dwType;
        public int dwDisplayType;
        public int dwUsage;
        public string? lpLocalName;
        public string? lpRemoteName;
        public string? lpComment;
        public string? lpProvider;
    }

    private const int RESOURCETYPE_DISK = 0x00000001;
    private const int CONNECT_TEMPORARY = 0x00000004;

    private const int NO_ERROR = 0;
    private const int ERROR_ACCESS_DENIED = 5;
    private const int ERROR_BAD_NETPATH = 53;
    private const int ERROR_BAD_NET_NAME = 67;
    private const int ERROR_ALREADY_ASSIGNED = 85;
    private const int ERROR_INVALID_PASSWORD = 86;
    private const int ERROR_SESSION_CREDENTIAL_CONFLICT = 1219;
    private const int ERROR_LOGON_FAILURE = 1326;

    [DllImport("mpr.dll", EntryPoint = "WNetAddConnection2W", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int WNetAddConnection2(
        ref NETRESOURCE lpNetResource,
        string? lpPassword,
        string? lpUsername,
        int dwFlags);

    [DllImport("mpr.dll", EntryPoint = "WNetCancelConnection2W", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int WNetCancelConnection2(
        string lpName,
        int dwFlags,
        [MarshalAs(UnmanagedType.Bool)] bool fForce);

    /// <summary>
    /// UNCパスかどうかを判定する
    /// </summary>
    public static bool IsUncPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        return path.StartsWith(@"\\") || path.StartsWith("//");
    }

    /// <summary>
    /// パスからUNC共有ルート（例: \\server\share）を抽出する
    /// </summary>
    public static string? ExtractUncShareRoot(string path)
    {
        if (!IsUncPath(path)) return null;

        var normalized = path.Replace('/', '\\');
        // 例: \\server\share または \\server\share\sub\dir
        var match = Regex.Match(normalized, @"^(\\\\[^\\]+\\[^\\]+)");
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        // \\server のみのケース
        var serverMatch = Regex.Match(normalized, @"^(\\\\[^\\]+)");
        return serverMatch.Success ? serverMatch.Groups[1].Value : normalized;
    }

    /// <summary>
    /// NASへの認証セッションを確立する
    /// </summary>
    public static (bool Success, string Message) Authenticate(string uncPath, string? username, string? password)
    {
        var shareRoot = ExtractUncShareRoot(uncPath);
        if (string.IsNullOrEmpty(shareRoot))
        {
            return (false, "有効なUNCパス (\\\\server\\share) ではありません。");
        }

        var netResource = new NETRESOURCE
        {
            dwType = RESOURCETYPE_DISK,
            lpRemoteName = shareRoot
        };

        var result = WNetAddConnection2(
            ref netResource,
            string.IsNullOrEmpty(password) ? null : password,
            string.IsNullOrEmpty(username) ? null : username,
            CONNECT_TEMPORARY);

        return result switch
        {
            NO_ERROR => (true, $"NAS ({shareRoot}) に正常に接続・認証しました。"),
            ERROR_SESSION_CREDENTIAL_CONFLICT => (true, $"NAS ({shareRoot}) は既存のセッションで接続済みです。(1219)"),
            ERROR_ALREADY_ASSIGNED => (true, $"NAS ({shareRoot}) は既にマウントまたは接続されています。(85)"),
            ERROR_ACCESS_DENIED => (false, $"NAS ({shareRoot}) へのアクセスが拒否されました (5)。権限を確認してください。"),
            ERROR_LOGON_FAILURE => (false, $"NAS ({shareRoot}) の認証に失敗しました (1326)。ユーザー名またはパスワードが正しくありません。"),
            ERROR_INVALID_PASSWORD => (false, $"NAS ({shareRoot}) のパスワードが不正です (86)。"),
            ERROR_BAD_NETPATH => (false, $"ネットワークパスが見つかりません (53)。サーバー名を確認してください。"),
            ERROR_BAD_NET_NAME => (false, $"共有名が見つかりません (67)。共有フォルダ名を確認してください。"),
            _ => (false, $"NAS接続エラー (エラーコード: {result})")
        };
    }

    /// <summary>
    /// 接続テスト非同期実行
    /// </summary>
    public static Task<(bool Success, string Message)> TestConnectionAsync(string uncPath, string? username, string? password)
    {
        return Task.Run(() =>
        {
            try
            {
                if (!IsUncPath(uncPath))
                {
                    // ローカルパスの場合
                    if (System.IO.Directory.Exists(uncPath))
                    {
                        return (true, $"ローカルフォルダ ({uncPath}) は正常にアクセス可能です。");
                    }
                    return (false, $"指定されたローカルフォルダ ({uncPath}) が存在しません。");
                }

                var (authSuccess, authMsg) = Authenticate(uncPath, username, password);
                if (!authSuccess)
                {
                    return (false, authMsg);
                }

                // ディレクトリ存在確認
                if (System.IO.Directory.Exists(uncPath))
                {
                    return (true, $"{authMsg}\nフォルダアクセス確認: 成功");
                }
                else
                {
                    return (true, $"{authMsg}\n(注意: 指定されたサブフォルダはまだ存在しませんが、接続は確立しました)");
                }
            }
            catch (Exception ex)
            {
                return (false, $"テスト実行中に例外が発生しました: {ex.Message}");
            }
        });
    }
}
