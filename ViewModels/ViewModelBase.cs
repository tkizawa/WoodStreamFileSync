using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace WoodStreamFileSync.ViewModels;

/// <summary>
/// MVVM パターンの ViewModel 基底クラス。<see cref="INotifyPropertyChanged"/> を実装します
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    /// <summary>
    /// プロパティ値が変更されたときに発生するイベント
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// プロパティ変更イベントを発火します
    /// </summary>
    /// <param name="propertyName">変更されたプロパティ名（CallerMemberNameにより自動取得）</param>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// フィールドの値を更新し、値が変更された場合は変更通知イベントを発火します
    /// </summary>
    /// <typeparam name="T">プロパティの型</typeparam>
    /// <param name="storage">バッキングフィールドへの参照</param>
    /// <param name="value">設定する新しい値</param>
    /// <param name="propertyName">プロパティ名（自動取得）</param>
    /// <returns>値が変更された場合は true、変更がなかった場合は false</returns>
    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(storage, value)) return false;
        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

/// <summary>
/// 同期デリゲートを実行する <see cref="ICommand"/> の標準実装クラス
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    /// <summary>
    /// パラメータを受け取る実行デリゲートを指定してコマンドを初期化します
    /// </summary>
    /// <param name="execute">コマンド実行時のアクション</param>
    /// <param name="canExecute">コマンド実行可否を判定するデリゲート（省略時は常に実行可）</param>
    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>
    /// パラメータなしの実行デリゲートを指定してコマンドを初期化します
    /// </summary>
    /// <param name="execute">コマンド実行時のアクション</param>
    /// <param name="canExecute">コマンド実行可否を判定するデリゲート</param>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute != null ? _ => canExecute() : null)
    {
    }

    /// <summary>
    /// コマンドの実行可否状態が変化した際に発生するイベント
    /// </summary>
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// <summary>
    /// 現在の状態でコマンドを実行可能かどうかを判定します
    /// </summary>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    /// <summary>
    /// コマンドを実行します
    /// </summary>
    public void Execute(object? parameter) => _execute(parameter);

    /// <summary>
    /// コマンドの実行可否の再評価を要求します
    /// </summary>
    public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
}

/// <summary>
/// 非同期タスクを実行する <see cref="ICommand"/> の実装クラス（二重実行防止機能付き）
/// </summary>
public class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Func<object?, bool>? _canExecute;
    private bool _isExecuting;

    /// <summary>
    /// パラメータを受け取る非同期タスクを指定してコマンドを初期化します
    /// </summary>
    /// <param name="execute">非同期実行タスク</param>
    /// <param name="canExecute">実行可否判定デリゲート</param>
    public AsyncRelayCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>
    /// パラメータなしの非同期タスクを指定してコマンドを初期化します
    /// </summary>
    /// <param name="execute">非同期実行タスク</param>
    /// <param name="canExecute">実行可否判定デリゲート</param>
    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute != null ? _ => canExecute() : null)
    {
    }

    /// <summary>
    /// コマンド実行可否の変化イベント
    /// </summary>
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// <summary>
    /// 実行中でなく、かつ canExecute 条件を満たす場合に実行可能と判定します
    /// </summary>
    public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);

    /// <summary>
    /// 非同期コマンドを実行します
    /// </summary>
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        _isExecuting = true;
        RaiseCanExecuteChanged();
        try
        {
            await _execute(parameter);
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// コマンドの実行可否の再評価を要求します
    /// </summary>
    public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
}

/// <summary>
/// 型付きパラメータを受け取る同期 <see cref="ICommand"/> 実装クラス
/// </summary>
/// <typeparam name="T">パラメータの型</typeparam>
public class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    /// <summary>
    /// 型付きコマンドを初期化します
    /// </summary>
    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;

    public void Execute(object? parameter) => _execute((T?)parameter);

    public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
}

/// <summary>
/// 型付きパラメータを受け取る非同期 <see cref="ICommand"/> 実装クラス
/// </summary>
/// <typeparam name="T">パラメータの型</typeparam>
public class AsyncRelayCommand<T> : ICommand
{
    private readonly Func<T?, Task> _execute;
    private readonly Func<T?, bool>? _canExecute;
    private bool _isExecuting;

    /// <summary>
    /// 型付き非同期コマンドを初期化します
    /// </summary>
    public AsyncRelayCommand(Func<T?, Task> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke((T?)parameter) ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        _isExecuting = true;
        RaiseCanExecuteChanged();
        try
        {
            await _execute((T?)parameter);
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
}
