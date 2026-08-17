using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using WoodStreamFileSync.Models;

namespace WoodStreamFileSync.Services;

public class LoggerService
{
    private static LoggerService? _instance;
    public static LoggerService Instance => _instance ??= new LoggerService();

    private readonly object _lock = new();
    private readonly List<SyncLogEntry> _logs = new();
    private const int MaxMemoryLogs = 1000;
    private readonly string _logDirectory;

    public event Action<SyncLogEntry>? LogReceived;

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
            // fallback if appdata inaccessible
        }
    }

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

        // 非同期または別スレッドでファイル書き込み
        WriteToFile(entry);

        // UI購読者へ通知
        try
        {
            LogReceived?.Invoke(entry);
        }
        catch
        {
            // UI通知時の例外を無視
        }
    }

    public void LogInfo(string message, string? source = null) => Log(LogLevel.Info, message, source);
    public void LogSuccess(string message, string? source = null) => Log(LogLevel.Success, message, source);
    public void LogWarning(string message, string? source = null) => Log(LogLevel.Warning, message, source);
    public void LogError(string message, string? source = null) => Log(LogLevel.Error, message, source);
    public void LogDebug(string message, string? source = null) => Log(LogLevel.Debug, message, source);

    public IReadOnlyList<SyncLogEntry> GetRecentLogs()
    {
        lock (_lock)
        {
            return _logs.ToArray();
        }
    }

    public void ClearMemoryLogs()
    {
        lock (_lock)
        {
            _logs.Clear();
        }
    }

    public string GetLogDirectory() => _logDirectory;

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
