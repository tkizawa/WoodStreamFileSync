using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WoodStreamFileSync.Models;

namespace WoodStreamFileSync.Services;

public class RobocopyResult
{
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string SummaryMessage { get; set; } = string.Empty;
    public string StandardOutput { get; set; } = string.Empty;
    public string StandardError { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
}

public class RobocopyRunner
{
    static RobocopyRunner()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
        catch { }
    }

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

    private static string NormalizePathForCmd(string path)
    {
        var p = path.Trim();
        if (p.EndsWith('\\') && !p.EndsWith(":\\") && !p.StartsWith(@"\\?\"))
        {
            p = p.TrimEnd('\\');
        }
        return p;
    }

    public static (bool Success, string Description) EvaluateExitCode(int code)
    {
        // Robocopy Exit Code bitmask:
        // 0: No changes
        // 1: Files copied
        // 2: Extra files deleted
        // 4: Mismatches detected
        // 8: Failed copies
        // 16: Serious error

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
