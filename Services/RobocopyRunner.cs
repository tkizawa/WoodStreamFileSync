using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WoodStreamFileSync.Models;

namespace WoodStreamFileSync.Services;

/// <summary>
/// Robocopy コマンドの実行結果を保持するデータクラス
/// </summary>
public class RobocopyResult
{
    /// <summary>
    /// 同期が成功（終了コードが8未満）したかどうか
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Robocopy プロセスの終了コード (ビットマスク)
    /// </summary>
    public int ExitCode { get; set; }

    /// <summary>
    /// 終了コードに基づいた日本語サマリーメッセージ
    /// </summary>
    public string SummaryMessage { get; set; } = string.Empty;

    /// <summary>
    /// Robocopy の標準出力テキスト全体
    /// </summary>
    public string StandardOutput { get; set; } = string.Empty;

    /// <summary>
    /// Robocopy の標準エラー出力テキスト全体
    /// </summary>
    public string StandardError { get; set; } = string.Empty;

    /// <summary>
    /// 同期処理にかかった所要時間
    /// </summary>
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Windows 標準の `robocopy.exe` を非同期プロセスとして呼び出し、フォルダ間同期を実行・制御するクラス
/// </summary>
public class RobocopyRunner
{
    /// <summary>
    /// 静的コンストラクタ。Shift-JIS等のコードページプロバイダーを登録します
    /// </summary>
    static RobocopyRunner()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
        catch { }
    }

    /// <summary>
    /// コマンドプロンプト / Robocopy 出力用の文字エンコーディング（CP932 / OEMコードページ）を取得します
    /// </summary>
    private static Encoding GetCmdEncoding()
    {
        try
        {
            return Encoding.GetEncoding(932);
        }
        catch
        {
            try
            {
                return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
            }
            catch
            {
                return Encoding.UTF8;
            }
        }
    }

    /// <summary>
    /// Robocopy を非同期実行して同期処理を行います
    /// </summary>
    /// <param name="sourcePath">同期元フォルダパス</param>
    /// <param name="destinationPath">同期先フォルダパス</param>
    /// <param name="options">Robocopy 実行オプション</param>
    /// <param name="onOutputLine">出力行を受信した際のコールバックアクション（リアルタイムログ用）</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>Robocopy 実行結果オブジェクト</returns>
    public async Task<RobocopyResult> RunAsync(
        string sourcePath,
        string destinationPath,
        RobocopyOptions options,
        Action<string>? onOutputLine = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        // 引数の組み立て
        var arguments = BuildArguments(sourcePath, destinationPath, options);
        var encoding = GetCmdEncoding();

        var startInfo = new ProcessStartInfo
        {
            FileName = "robocopy.exe",
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = encoding,
            StandardErrorEncoding = encoding
        };

        LoggerService.Instance.LogInfo($"Robocopy コマンド開始: robocopy {arguments}", "Robocopy");

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        // 標準出力の非同期読み取りハンドラ
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
                onOutputLine?.Invoke(e.Data);
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    LoggerService.Instance.LogDebug(e.Data, "Robocopy.Out");
                }
            }
        };

        // 標準エラー出力の非同期読み取りハンドラ
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
                LoggerService.Instance.LogWarning(e.Data, "Robocopy.Err");
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);

            var duration = DateTime.Now - startTime;
            var exitCode = process.ExitCode;
            var (success, summary) = EvaluateExitCode(exitCode);

            var result = new RobocopyResult
            {
                Success = success,
                ExitCode = exitCode,
                SummaryMessage = summary,
                StandardOutput = outputBuilder.ToString(),
                StandardError = errorBuilder.ToString(),
                Duration = duration
            };

            if (success)
            {
                LoggerService.Instance.LogSuccess($"同期完了: {summary} (終了コード: {exitCode}, 所要時間: {duration.TotalSeconds:F1}秒)", "Robocopy");
            }
            else
            {
                LoggerService.Instance.LogError($"同期エラー: {summary} (終了コード: {exitCode}, 所要時間: {duration.TotalSeconds:F1}秒)", "Robocopy");
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            // キャンセル要求時のプロセス強制終了
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                }
            }
            catch { }

            LoggerService.Instance.LogWarning("同期処理がユーザーまたはシステムによってキャンセルされました。", "Robocopy");
            return new RobocopyResult
            {
                Success = false,
                ExitCode = -1,
                SummaryMessage = "同期がキャンセルされました。",
                Duration = DateTime.Now - startTime
            };
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"Robocopy プロセス実行時例外: {ex.Message}", "Robocopy");
            return new RobocopyResult
            {
                Success = false,
                ExitCode = -1,
                SummaryMessage = $"実行例外: {ex.Message}",
                Duration = DateTime.Now - startTime
            };
        }
    }

    /// <summary>
    /// 指定されたオプションから Robocopy のコマンドライン引数文字列を構築します
    /// </summary>
    /// <param name="source">同期元フォルダパス</param>
    /// <param name="destination">同期先フォルダパス</param>
    /// <param name="options">オプション設定</param>
    /// <returns>コマンドライン引数文字列</returns>
    private static string BuildArguments(string source, string destination, RobocopyOptions options)
    {
        // パス末尾のバックスラッシュがクォートと衝突してエスケープ扱いになるのを防ぐ (例: "C:\folder\" -> "C:\folder\\")
        var cleanSource = NormalizePathForCmd(source);
        var cleanDest = NormalizePathForCmd(destination);

        var args = new StringBuilder();
        args.Append($"\"{cleanSource}\" \"{cleanDest}\"");

        if (options.IsMirror)
        {
            args.Append(" /MIR");
        }
        else if (options.IncludeEmptySubdirectories)
        {
            args.Append(" /E");
        }

        args.Append($" /R:{Math.Max(0, options.RetryCount)}");
        args.Append($" /W:{Math.Max(0, options.WaitTimeSeconds)}");

        // 進行状況パーセント出力を抑制（ログが大量行になるのを防ぐ）
        args.Append(" /NP");

        // ファイル除外
        if (!string.IsNullOrWhiteSpace(options.ExcludeFiles))
        {
            args.Append($" /XF {options.ExcludeFiles.Trim()}");
        }

        // ディレクトリ除外
        if (!string.IsNullOrWhiteSpace(options.ExcludeDirs))
        {
            args.Append($" /XD {options.ExcludeDirs.Trim()}");
        }

        // 追加引数
        if (!string.IsNullOrWhiteSpace(options.AdditionalArguments))
        {
            args.Append($" {options.AdditionalArguments.Trim()}");
        }

        return args.ToString();
    }

    /// <summary>
    /// コマンド引数として渡すパス文字列を正規化します（末尾の余分なバックスラッシュを除去）
    /// </summary>
    /// <param name="path">正規化するパス文字列</param>
    /// <returns>正規化後のパス文字列</returns>
    private static string NormalizePathForCmd(string path)
    {
        var p = path.Trim();
        if (p.EndsWith('\\') && !p.EndsWith(":\\") && !p.StartsWith(@"\\?\"))
        {
            p = p.TrimEnd('\\');
        }
        return p;
    }

    /// <summary>
    /// Robocopy のビットマスク終了コードを解析し、成功成否および日本語の説明文を返します
    /// </summary>
    /// <param name="code">Robocopy 終了コード</param>
    /// <returns>成功フラグと説明文のタプル</returns>
    public static (bool Success, string Description) EvaluateExitCode(int code)
    {
        // Robocopy Exit Code bitmask:
        // 0: 変更なし（差分なし）
        // 1: ファイルコピー完了
        // 2: 余分なファイル削除完了
        // 4: 不一致ファイルの検出・同期
        // 8: コピー失敗（アクセス拒否など）
        // 16: 重大なエラー（構文エラーや致命的アクセス不可）

        if (code >= 16)
        {
            return (false, "重大なエラーが発生しました (アクセス不可、パス不正、構文エラーなど)");
        }
        if (code >= 8)
        {
            return (false, "1つ以上のファイル/フォルダのコピーに失敗しました (アクセス拒否やファイルロック等)");
        }

        return code switch
        {
            0 => (true, "変更はありません (完全同期済み)"),
            1 => (true, "ファイルのコピーが正常に完了しました"),
            2 => (true, "余分なファイル/フォルダが正常に削除されました"),
            3 => (true, "ファイルのコピーおよび余分なファイルの削除が正常に完了しました"),
            4 => (true, "一部ファイルの違いを検出・同期しました"),
            5 => (true, "ファイルコピーおよび差分更新が完了しました"),
            6 => (true, "ファイル削除および差分更新が完了しました"),
            7 => (true, "全ファイルの同期・削除・差分更新が完了しました"),
            _ => (true, $"同期が完了しました (コード: {code})")
        };
    }
}
