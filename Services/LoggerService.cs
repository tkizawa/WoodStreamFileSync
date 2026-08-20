using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using WoodStreamFileSync.Models;

namespace WoodStreamFileSync.Services;

/// <summary>
/// アプリケーションログのインメモリ蓄積および日別ログファイル出力（%LocalAppData%\WoodStreamFileSync\logs）を管理するシングルトンサービスクラス
/// </summary>
public class LoggerService
{
    private static LoggerService? _instance;

    /// <summary>
    /// <see cref="LoggerService"/> のシングルトンインスタンス
    /// </summary>
    public static LoggerService Instance => _instance ??= new LoggerService();

    /// <summary>
    /// スレッド同期用ロックオブジェクト
    /// </summary>
    private readonly object _lock = new();

    /// <summary>
    /// メモリ上に保持するログリスト
    /// </summary>
    private readonly List<SyncLogEntry> _logs = new();

    /// <summary>
    /// メモリ上に保持する最大ログ件数
    /// </summary>
    private const int MaxMemoryLogs = 1000;

    /// <summary>
    /// ログファイルの保存先ディレクトリパス
    /// </summary>
    private readonly string _logDirectory;

    /// <summary>
    /// 新しいログが記録された際に発生するイベント（UIのリアルタイム更新用）
    /// </summary>
    public event Action<SyncLogEntry>? LogReceived;

    /// <summary>
    /// <see cref="LoggerService"/> クラスの新しいインスタンスを初期化し、ログ保存フォルダを作成します
    /// </summary>
    public LoggerService()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _logDirectory = Path.Combine(localAppData, "WoodStreamFileSync", "logs");
        try
        {
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }
        }
        catch
        {
            // ディレクトリ作成失敗時はフォールバック
        }
    }

    /// <summary>
    /// ログエントリを記録し、メモリ保持、ファイル出力、イベント通知を行います
    /// </summary>
    /// <param name="level">ログレベル</param>
    /// <param name="message">ログメッセージ本文</param>
    /// <param name="source">ログ発生元コンポーネント名（省略可）</param>
    public void Log(LogLevel level, string message, string? source = null)
    {
        var entry = new SyncLogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message,
            Source = source
        };

        lock (_lock)
        {
            _logs.Add(entry);
            if (_logs.Count > MaxMemoryLogs)
            {
                _logs.RemoveAt(0);
            }
        }

        // 日別ログファイルへの追記書き込み
        WriteToFile(entry);

        // UI購読者へ通知
        try
        {
            LogReceived?.Invoke(entry);
        }
        catch
        {
            // UI通知時の例外を握りつぶす
        }
    }

    /// <summary>
    /// 情報ログを記録します
    /// </summary>
    public void LogInfo(string message, string? source = null) => Log(LogLevel.Info, message, source);

    /// <summary>
    /// 成功ログを記録します
    /// </summary>
    public void LogSuccess(string message, string? source = null) => Log(LogLevel.Success, message, source);

    /// <summary>
    /// 警告ログを記録します
    /// </summary>
    public void LogWarning(string message, string? source = null) => Log(LogLevel.Warning, message, source);

    /// <summary>
    /// エラーログを記録します
    /// </summary>
    public void LogError(string message, string? source = null) => Log(LogLevel.Error, message, source);

    /// <summary>
    /// デバッグログを記録します
    /// </summary>
    public void LogDebug(string message, string? source = null) => Log(LogLevel.Debug, message, source);

    /// <summary>
    /// 現在メモリに保持されている直近のログリストを取得します
    /// </summary>
    /// <returns>ログエントリの読み取り専用リスト</returns>
    public IReadOnlyList<SyncLogEntry> GetRecentLogs()
    {
        lock (_lock)
        {
            return _logs.ToArray();
        }
    }

    /// <summary>
    /// メモリ上のログ履歴をクリアします
    /// </summary>
    public void ClearMemoryLogs()
    {
        lock (_lock)
        {
            _logs.Clear();
        }
    }

    /// <summary>
    /// ログファイルが保存されるディレクトリパスを取得します
    /// </summary>
    /// <returns>ログフォルダパス</returns>
    public string GetLogDirectory() => _logDirectory;

    /// <summary>
    /// 日付ごとのログファイル (sync_yyyyMMdd.log) にUTF-8テキストでログを追記します
    /// </summary>
    /// <param name="entry">書き込むログエントリ</param>
    private void WriteToFile(SyncLogEntry entry)
    {
        try
        {
            var fileName = $"sync_{DateTime.Now:yyyyMMdd}.log";
            var filePath = Path.Combine(_logDirectory, fileName);
            var logLine = entry.ToString() + Environment.NewLine;
            File.AppendAllText(filePath, logLine, Encoding.UTF8);
        }
        catch
        {
            // ファイルIO例外は握りつぶす
        }
    }
}
