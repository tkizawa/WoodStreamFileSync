using System;
using System.IO;
using System.Windows.Input;
using Microsoft.Win32;
using WoodStreamFileSync.Models;
using WoodStreamFileSync.Services;

namespace WoodStreamFileSync.ViewModels;

public class FolderPairViewModel : ViewModelBase
{
    private string _id = Guid.NewGuid().ToString("N");
    private string _name = string.Empty;
    private string _sourcePath = string.Empty;
    private string _destinationPath = string.Empty;
    private bool _isEnabled = true;
    private DateTime? _lastSyncTime;
    private SyncStatus _lastSyncStatus = SyncStatus.Idle;

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    public string SourcePath
    {
        get => _sourcePath;
        set
        {
            if (SetProperty(ref _sourcePath, value))
            {
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    public string DestinationPath
    {
        get => _destinationPath;
        set => SetProperty(ref _destinationPath, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public DateTime? LastSyncTime
    {
        get => _lastSyncTime;
        set => SetProperty(ref _lastSyncTime, value);
    }

    public SyncStatus LastSyncStatus
    {
        get => _lastSyncStatus;
        set => SetProperty(ref _lastSyncStatus, value);
    }

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

            return LocalizationService.Instance.IsJapanese ? "新規フォルダペア" : "New Folder Pair";
        }
    }

    public ICommand BrowseSourceCommand { get; }
    public ICommand BrowseDestinationCommand { get; }

    public FolderPairViewModel()
    {
        BrowseSourceCommand = new RelayCommand(BrowseSource);
        BrowseDestinationCommand = new RelayCommand(BrowseDestination);
    }

    public FolderPairViewModel(SyncFolderPair model) : this()
    {
        _id = model.Id;
        _name = model.Name;
        _sourcePath = model.SourcePath;
        _destinationPath = model.DestinationPath;
        _isEnabled = model.IsEnabled;
        _lastSyncTime = model.LastSyncTime;
        _lastSyncStatus = model.LastSyncStatus;
    }

    public SyncFolderPair ToModel()
    {
        return new SyncFolderPair
        {
            Id = this.Id,
            Name = this.Name.Trim(),
            SourcePath = this.SourcePath.Trim(),
            DestinationPath = this.DestinationPath.Trim(),
            IsEnabled = this.IsEnabled,
            LastSyncTime = this.LastSyncTime,
            LastSyncStatus = this.LastSyncStatus
        };
    }

    private void BrowseSource()
    {
        var loc = LocalizationService.Instance;
        var dialog = new OpenFolderDialog
        {
            Title = loc.GetString("Settings.SourceDialogTitle"),
            InitialDirectory = Directory.Exists(SourcePath) ? SourcePath : ""
        };
        if (dialog.ShowDialog() == true)
        {
            SourcePath = dialog.FolderName;
        }
    }

    private void BrowseDestination()
    {
        var loc = LocalizationService.Instance;
        var dialog = new OpenFolderDialog
        {
            Title = loc.GetString("Settings.DestDialogTitle"),
            InitialDirectory = Directory.Exists(DestinationPath) ? DestinationPath : ""
        };
        if (dialog.ShowDialog() == true)
        {
            DestinationPath = dialog.FolderName;
        }
    }
}
