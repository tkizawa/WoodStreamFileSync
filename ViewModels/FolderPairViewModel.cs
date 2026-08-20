using System;
using System.IO;
using System.Windows.Input;
using Microsoft.Win32;
using WoodStreamFileSync.Models;
using WoodStreamFileSync.Services;

namespace WoodStreamFileSync.ViewModels;

/// <summary>
/// 設定画面等で1件の同期フォルダペアの表示・編集を担う ViewModel
/// </summary>
public class FolderPairViewModel : ViewModelBase
{
    private string _id = Guid.NewGuid().ToString("N");
    private string _name = string.Empty;
    private string _sourcePath = string.Empty;
    private string _destinationPath = string.Empty;
    private bool _isEnabled = true;
    private DateTime? _lastSyncTime;
    private SyncStatus _lastSyncStatus = SyncStatus.Idle;

    /// <summary>
    /// フォルダペアの一意な識別ID
    /// </summary>
    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    /// <summary>
    /// ユーザー設定のカスタム表示名
    /// </summary>
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

    /// <summary>
    /// 同期元のフォルダパス
    /// </summary>
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

    /// <summary>
    /// 同期先のフォルダパス
    /// </summary>
    public string DestinationPath
    {
        get => _destinationPath;
        set => SetProperty(ref _destinationPath, value);
    }

    /// <summary>
    /// このフォルダペアの同期を有効にするかどうか
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    /// <summary>
    /// 最終同期日時
    /// </summary>
    public DateTime? LastSyncTime
    {
        get => _lastSyncTime;
        set => SetProperty(ref _lastSyncTime, value);
    }

    /// <summary>
    /// 最終同期ステータス
    /// </summary>
    public SyncStatus LastSyncStatus
    {
        get => _lastSyncStatus;
        set => SetProperty(ref _lastSyncStatus, value);
    }

    /// <summary>
    /// UIリスト表示用の名称（Name未設定時はフォルダパスから算出）
    /// </summary>
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

    /// <summary>
    /// 同期元フォルダ参照ダイアログを開くコマンド
    /// </summary>
    public ICommand BrowseSourceCommand { get; }

    /// <summary>
    /// 同期先フォルダ参照ダイアログを開くコマンド
    /// </summary>
    public ICommand BrowseDestinationCommand { get; }

    /// <summary>
    /// <see cref="FolderPairViewModel"/> の新しいインスタンスを初期化します
    /// </summary>
    public FolderPairViewModel()
    {
        BrowseSourceCommand = new RelayCommand(BrowseSource);
        BrowseDestinationCommand = new RelayCommand(BrowseDestination);
    }

    /// <summary>
    /// データモデル <see cref="SyncFolderPair"/> から ViewModel を初期化します
    /// </summary>
    /// <param name="model">初期化元のデータモデル</param>
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

    /// <summary>
    /// 現在の編集内容を <see cref="SyncFolderPair"/> モデルに変換して返します
    /// </summary>
    /// <returns>変換されたデータモデル</returns>
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

    /// <summary>
    /// 同期元フォルダ選択用の OpenFolderDialog を表示します
    /// </summary>
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

    /// <summary>
    /// 同期先フォルダ選択用の OpenFolderDialog を表示します
    /// </summary>
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
