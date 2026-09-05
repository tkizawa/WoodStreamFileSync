using System;
using System.Windows;
using WoodStreamFileSync.Models;

namespace WoodStreamFileSync.Services;

/// <summary>
/// GUIウィンドウの位置およびサイズの保存と復元を行うヘルパークラス
/// プロジェクトルール: 「GUI アプリケーションの場合は、終了時のウィンドウ位置およびサイズを保存し、次回起動時に復元すること」
/// </summary>
public static class WindowPlacementHelper
{
    /// <summary>
    /// 設定情報をもとにウィンドウの位置・サイズを復元します。
    /// マルチモニター環境の切断等で画面外に表示されてしまうケースを防止するため、仮想スクリーン内に収まっているか検証します。
    /// </summary>
    /// <param name="window">対象ウィンドウ</param>
    /// <param name="placement">保存されていた配置情報</param>
    public static void RestorePlacement(Window window, WindowPlacementConfig? placement)
    {
        if (placement == null)
        {
            return;
        }

        // 最小幅・高さが指定されている場合のガード
        if (placement.Width > 0 && placement.Height > 0)
        {
            window.Width = placement.Width;
            window.Height = placement.Height;
        }

        // 保存された位置が現在の仮想画面領域内に含まれるかチェック
        // ウィンドウの少なくとも一部（あるいは左上座標）が見える範囲にあることを確認
        double vLeft = SystemParameters.VirtualScreenLeft;
        double vTop = SystemParameters.VirtualScreenTop;
        double vWidth = SystemParameters.VirtualScreenWidth;
        double vHeight = SystemParameters.VirtualScreenHeight;

        bool isVisibleOnScreen =
            placement.Left >= vLeft - 20 &&
            placement.Left <= (vLeft + vWidth - 50) &&
            placement.Top >= vTop - 20 &&
            placement.Top <= (vTop + vHeight - 50);

        if (isVisibleOnScreen)
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = placement.Left;
            window.Top = placement.Top;
        }

        // 最大化状態だった場合は最大化
        if (placement.IsMaximized)
        {
            window.WindowState = WindowState.Maximized;
        }
    }

    /// <summary>
    /// 現在のウィンドウ位置・サイズを取得して WindowPlacementConfig を生成します。
    /// 最大化されている場合は、元の通常サイズ・位置（RestoreBounds）を保存します。
    /// </summary>
    /// <param name="window">対象ウィンドウ</param>
    /// <returns>ウィンドウ配置情報</returns>
    public static WindowPlacementConfig CapturePlacement(Window window)
    {
        var placement = new WindowPlacementConfig
        {
            IsMaximized = window.WindowState == WindowState.Maximized
        };

        if (window.WindowState == WindowState.Maximized)
        {
            // 最大化時は通常表示時の境界矩形（RestoreBounds）を取得して保存
            placement.Left = window.RestoreBounds.Left;
            placement.Top = window.RestoreBounds.Top;
            placement.Width = window.RestoreBounds.Width;
            placement.Height = window.RestoreBounds.Height;
        }
        else
        {
            placement.Left = window.Left;
            placement.Top = window.Top;
            placement.Width = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
            placement.Height = window.ActualHeight > 0 ? window.ActualHeight : window.Height;
        }

        return placement;
    }
}
