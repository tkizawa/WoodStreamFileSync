using System;
using System.IO;
using System.Text.Json.Serialization;

namespace WoodStreamFileSync.Models;

public class SyncFolderPair
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;

    [JsonIgnore]
    public DateTime? LastSyncTime { get; set; }

    [JsonIgnore]
    public SyncStatus LastSyncStatus { get; set; } = SyncStatus.Idle;

    [JsonIgnore]
    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Name))
                return Name;

            if (!string.IsNullOrWhiteSpace(SourcePath))
            {
                try
                {
                    var dirName = Path.GetFileName(SourcePath.TrimEnd('\\', '/'));
                    if (!string.IsNullOrEmpty(dirName))
                        return dirName;
                }
                catch { }
                return SourcePath;
            }

            return "新規フォルダペア";
        }
    }

    public SyncFolderPair Clone()
    {
        return new SyncFolderPair
        {
            Id = this.Id,
            Name = this.Name,
            SourcePath = this.SourcePath,
            DestinationPath = this.DestinationPath,
            IsEnabled = this.IsEnabled,
            LastSyncTime = this.LastSyncTime,
            LastSyncStatus = this.LastSyncStatus
        };
    }
}
