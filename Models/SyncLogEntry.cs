using System;

namespace WoodStreamFileSync.Models;

public enum LogLevel
{
    Info,
    Success,
    Warning,
    Error,
    Debug
}

public class SyncLogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public LogLevel Level { get; set; } = LogLevel.Info;
    public string Message { get; set; } = string.Empty;
    public string? Source { get; set; }

    public string FormattedTimestamp => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");

    public override string ToString()
    {
        var prefix = Source != null ? $"[{Source}] " : "";
        return $"[{FormattedTimestamp}] [{Level.ToString().ToUpperInvariant()}] {prefix}{Message}";
    }
}
