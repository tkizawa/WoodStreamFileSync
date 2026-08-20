using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using WoodStreamFileSync.Models;
using WoodStreamFileSync.Services;

namespace WoodStreamFileSync.ViewModels;

/// <summary>
/// ログ確認ウィンドウのデータバインディングおよび操作を担う ViewModel
/// </summary>
public class LogViewModel : ViewModelBase
{
    private readonly LoggerService _logger;
    private bool _autoScroll = true;
    private readonly ConcurrentQueue<SyncLogEntry> _pendingLogs = new();
    private readonly DispatcherTimer _uiUpdateTimer;

    /// <summary>
    /// UIにバインドされるログレコードのコレクション
    /// </summary>
    public ObservableCollection<SyncLogEntry> Logs { get; } = new();

    /// <summary>
    /// 新規ログ受信時に最下部へ自動スクロールするかどうか
    /// </summary>
    public bool AutoScroll
    {
        get => _autoScroll;
        set => SetProperty(ref _autoScroll, value);
    }

    /// <summary>
    /// ログ消去コマンド
    /// </summary>
    public ICommand ClearLogsCommand { get; }

    /// <summary>
    /// ログ全文クリップボードコピーコマンド
    /// </summary>
    public ICommand CopyLogsCommand { get; }

    /// <summary>
    /// ログ保存フォルダをエクスプローラーで開くコマンド
    /// </summary>
    public ICommand OpenLogFolderCommand { get; }

    /// <summary>
    /// <see cref="LogViewModel"/> の新しいインスタンスを初期化し、直近ログの読み込みとUIバッチ更新タイマーを開始します
    /// </summary>
    /// <param name="logger">ロガーサービスインスタンス</param>
    public LogViewModel(LoggerService logger)
    {
        _logger = logger;

        // 既存の直近ログを読み込み
        foreach (var entry in _logger.GetRecentLogs())
        {
            Logs.Add(entry);
        }

        _logger.LogReceived += OnLogReceived;

        // UIスレッドを飽和させないためのバッチ更新タイマー (100msごとにまとめてUIに追加)
        _uiUpdateTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _uiUpdateTimer.Tick += OnUiUpdateTick;
        _uiUpdateTimer.Start();

        ClearLogsCommand = new RelayCommand(ClearLogs);
        CopyLogsCommand = new RelayCommand(CopyLogs);
        OpenLogFolderCommand = new RelayCommand(OpenLogFolder);
    }

    /// <summary>
    /// バックグラウンドからのログ受信時にスレッドセーフなキューへ追加します
    /// </summary>
    /// <param name="entry">受信したログレコード</param>
    private void OnLogReceived(SyncLogEntry entry)
    {
        _pendingLogs.Enqueue(entry);
    }

    /// <summary>
    /// 100msごとのUIタイマータスク。キューからログを取り出して ObservableCollection にバッチ追加します
    /// </summary>
    private void OnUiUpdateTick(object? sender, EventArgs e)
    {
        if (_pendingLogs.IsEmpty) return;

        int addedCount = 0;
        while (_pendingLogs.TryDequeue(out var entry) && addedCount < 100)
        {
            Logs.Add(entry);
            addedCount++;
        }

        // メモリ圧迫防止のためコレクションサイズを最大1500行に制限
        while (Logs.Count > 1500)
        {
            Logs.RemoveAt(0);
        }
    }

    /// <summary>
    /// 画面およびメモリ上のログを消去します
    /// </summary>
    private void ClearLogs()
    {
        _logger.ClearMemoryLogs();
        while (_pendingLogs.TryDequeue(out _)) { }
        Logs.Clear();
    }

    /// <summary>
    /// 表示中のログテキスト全体をクリップボードにコピーします
    /// </summary>
    private void CopyLogs()
    {
        var sb = new StringBuilder();
        foreach (var entry in Logs)
        {
            sb.AppendLine(entry.ToString());
        }
        Clipboard.SetText(sb.ToString());
        MessageBox.Show("ログをクリップボードにコピーしました。", "コピー完了", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>
    /// ログファイルが保存されているローカルフォルダを Windows エクスプローラーで開きます
    /// </summary>
    private void OpenLogFolder()
    {
        try
        {
            var dir = _logger.GetLogDirectory();
            if (Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ログフォルダを開けませんでした: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
