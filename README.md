# WoodStreamFileSync
**Windows向け バックグラウンドフォルダ同期ツール (Robocopy + NAS認証 + リアルタイム変更検知)**

![.NET 10](https://img.shields.io/badge/.NET-10.0-blue)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011-blue)
![License](https://img.shields.io/badge/License-MIT-green)

---

## 📖 概要
**WoodStreamFileSync** は、Windows上でタスクトレイに常駐し、指定したローカルフォルダまたはネットワークフォルダ（NAS・ファイルサーバー）間を高信頼な Windows 標準コマンド `robocopy.exe` を用いてバックグラウンド自動同期するデスクトップアプリケーションです。

### 🌟 主な特長
- **🚀 タスクトレイ常駐 & 軽量設計**: 普段はタスクトレイに常駐。必要な時のみ設定画面やログ画面を表示。
- **🌐 日本語 & 英語 完全対応 (Multilingual)**:
  - Windows OS の言語（`CultureInfo.CurrentUICulture`）を自動検知して日本語/英語で表示。
  - アプリ設定画面から「Windowsの言語に追従」「日本語 (Japanese)」「English (英語)」を手動で即座に切り替え可能。
- **🌓 Windows ダーク/ライトモード両対応**:
  - Windows OS のテーマ設定（ダーク/ライト）に自動連動。
  - アプリ設定画面から「Windowsに追従」「ライトモード」「ダークモード」をいつでも切り替え可能。
  - DWM API (`DwmSetWindowAttribute`) によるダークタイトルバーにも対応。
- **⏱️ ハイブリッド同期トリガー**:
  - **定期タイマー同期**: 5分〜120分の指定間隔で定期的に同期。
  - **リアルタイム変更検知**: `FileSystemWatcher` によるファイルの追加・変更・削除・リネームを検知。
  - **デバウンス制御**: 連続書き込みや大量ファイルコピー中に多重起動しないよう、変更が落ち着いてから1回だけ同期を実行。
- **🔐 NAS / ネットワーク事前認証**:
  - UNCパス（`\\server\share`）への接続時、同期直前に Windows API (`mpr.dll` の `WNetAddConnection2`) を用いてセッションを自動確立。
  - パスワードは Windows DPAPI (`ProtectedData`) により暗号化されて安全に保存。
  - UIからワンクリックで接続検証できる「接続テスト」機能。
- **🛡️ 堅牢な排他制御 & エラーハンドリング**:
  - `SemaphoreSlim` による排他制御で、手動実行・タイマー・リアルタイム検知が重複しない安全設計。
  - Robocopy 特有の終了コード（0〜7: 成功 / 8以上: エラー）を正確に判定し、エラー時はトレイ通知を発行。
- **📊 リアルタイムログビューア**:
  - 詳細な実行ログ、ステータス、差分コピー結果をリアルタイム表示。日別ログファイル自動保存にも対応。

---

## 🛠️ システム要件
- **OS**: Windows 10 (version 1809以降) / Windows 11
- **ランタイム**: .NET 10.0 Windows Desktop Runtime (または自己完結型シングルファイル発行)

---

## 🏗️ プロジェクト構造
```
c:\Dev\WoodStreamFileSync\
├── WoodStreamFileSync.csproj       # プロジェクト定義 (.NET 10, WPF, H.NotifyIcon)
├── App.xaml / App.xaml.cs          # アプリケーションエントリ、トレイアイコン、Mutex単一インスタンス
├── Models/
│   ├── AppConfig.cs                # 設定データモデル (Robocopyオプション含む)
│   ├── SyncLogEntry.cs             # ログモデル (LogLevel, Timestamp, Source, Message)
│   └── SyncStatus.cs               # 同期ステータス (Idle, Syncing, Success, Warning, Error)
├── Services/
│   ├── ConfigManager.cs            # JSON設定管理 + DPAPI暗号化/復号 + スタートアップ登録
│   ├── NasAuthenticator.cs        # WNetAddConnection2 によるNAS認証 & 接続テスト
│   ├── RobocopyRunner.cs           # Robocopy非同期実行・引数構築・終了コード判定
│   ├── FolderWatcherService.cs     # FileSystemWatcher + デバウンスタイマー
│   ├── SyncManager.cs              # 排他制御 SemaphoreSlim、同期調停、トレイ通知
│   └── LoggerService.cs            # ログイベント配信 + 日別ログファイル出力
├── ViewModels/
│   ├── ViewModelBase.cs            # MVVM基底クラス & RelayCommand / AsyncRelayCommand
│   ├── SettingsViewModel.cs        # 設定ウィンドウ ViewModel
│   └── LogViewModel.cs             # ログビューア ViewModel
├── Views/
│   ├── Converters.cs               # XAMLバリューコンバーター & PasswordBoxHelper
│   ├── SettingsWindow.xaml / .cs   # モダン設定ウィンドウ
│   └── LogWindow.xaml / .cs        # リアルタイムログビューア
├── Resources/                      # アイコンリソース
└── WoodStreamFileSync.Tests/       # 単体テスト / 結合テスト (xUnit)
```

---

## 🚀 ビルド & 実行手順

### 1. 開発モードでのビルド & 実行
```powershell
# 依存関係の復元とビルド
dotnet build

# アプリケーションの起動
dotnet run
```

### 2. テストの実行
```powershell
dotnet test WoodStreamFileSync.Tests/WoodStreamFileSync.Tests.csproj
```

### 3. リリース用シングルファイル発行 (単一 exe)
.NET ランタイムが未インストールの環境でも動く自己完結型 (Self-Contained) の単一実行ファイルを出力する場合：
```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish
```

---

## 📋 操作マニュアル

### 1. タスクトレイでの基本操作
- **アイコンダブルクリック**: 「設定」ウィンドウを開きます。
- **アイコン右クリックメニュー**:
  - **⚡ 今すぐ同期**: 即座に同期処理を実行します。
  - **👁️ リアルタイム監視の有効/無効**: ファイル変更検知のオン/オフをワンクリックで切り替えます。
  - **⚙️ 設定...**: 設定画面を表示します。
  - **📋 ログ表示...**: リアルタイム動作ログ画面を表示します。
  - **🚪 終了**: バックグラウンド常駐を解除し、アプリを終了します。

### 2. 設定画面の各項目
| 項目グループ | 設定項目 | 説明 |
| :--- | :--- | :--- |
| **📁 同期フォルダ** | 同期元 (Source) | コピー元のフォルダパス（「参照...」ボタン対応） |
| | 同期先 (Dest) | コピー先のフォルダパス（ローカルパス または UNCパス `\\server\share`） |
| **⏱️ 同期モード** | 定期同期 | チェックを入れると、指定間隔（5, 10, 15, 30, 60, 120分）で定期同期 |
| | リアルタイム検知 | チェックを入れると、ファイルの作成・更新・削除・名前変更を検知して自動同期 |
| | 変更後待機 (秒) | ファイル連続書き込みや大量ファイル操作が落ち着くまで待機するデバウンス秒数（既定: 10秒） |
| **🔐 NAS認証** | 有効トグル | UNCパス接続時に事前セッション認証を行う場合に有効化 |
| | ユーザー名 / パスワード | NASのログイン資格情報（パスワードは Windows DPAPI で暗号化保存） |
| | 接続テスト | 設定したパスと資格情報で即座に接続できるかをテスト |
| **⚙️ Robocopyオプション** | ミラーリング (`/MIR`) | ソースに存在しないファイルを同期先から自動削除（完全同期） |
| | 空サブフォルダ含む (`/E`) | 空のディレクトリも含めて同期 |
| | リトライ回数 (`/R`) / 待機 (`/W`) | ファイルロック時などのリトライ設定（既定: 各1回） |
| | 除外ファイル (`/XF`) / フォルダ (`/XD`) | 同期対象外とするファイルやフォルダ（例: `*.tmp`, `.git` など） |
| | 追加引数 | Robocopy に直接渡す任意のオプション（例: `/FFT /MT:8` など） |
| **💻 アプリケーション** | スタートアップ起動 | Windows ログイン時に自動起動して常駐 |
| | 閉じるボタンの動作 | `[X]` ボタン押下時に終了せずタスクトレイに最小化 |

---

## 🔒 セキュリティ
- **Windows DPAPI (Data Protection API)**:
  - パスワードは `ProtectedData.Protect` (スコープ: `CurrentUser`) で暗号化された状態で `%APPDATA%\WoodStreamFileSync\config.json` に保存されます。
  - 他のユーザーや別PCに設定ファイルをコピーしても復号できないため、安全に運用できます。

---

## 📄 ライセンス
MIT License
