# Microsoft Store 登録用情報 (Store Listing Information)

Microsoft Partner Center（パートナーセンター）の **Store Listings（ストア登録情報）** 入力用テンプレートです。
日本語（ja-JP）および英語（en-US）の登録に必要な全項目をまとめています。

---

# 🇯🇵 日本語 (Japanese - ja-JP)

### 1. 製品名 (Product Name)
```text
WoodStream FileSync
```

### 2. 簡単な説明 (Short Description)
*※ 検索結果やタイル、アプリ概要欄に表示される1〜2文の説明（250文字以内）*
```text
Windowsタスクトレイに常駐し、NASやローカルフォルダ間を高信頼Robocopyで自動バックアップ・同期する軽量デスクトップツール。リアルタイム変更検知や定期同期、NAS認証に対応。
```

### 3. 詳細説明 (Description)
*※ 最大10,000文字。Markdownではなくプレーンテキストで貼り付けます。*
```text
WoodStream FileSync は、Windows 標準の高信頼同期エンジン「Robocopy」をベースにした、バックグラウンド自動フォルダ同期・NASバックアップツールです。

タスクトレイに常駐し、大切な仕事のファイルや写真、ドキュメントを、NAS（ネットワーク接続ストレージ）や外付けHDD、別ドライブへ確実に自動バックアップします。

【🌟 主な特長】

◆ 🚀 タスクトレイ常駐 & 軽量・省リソース設計
普段はタスクトレイ（通知領域）に格納され、CPUやメモリを無駄に消費しません。ウィンドウの閉じるボタンを押してもバックグラウンドで動作し続けます。

◆ 📁 複数フォルダペアの個別管理
同期元・同期先のペアを複数登録可能。ペアごとに有効/無効の切り替えや個別手動同期、削除を直感的に操作できます。

◆ ⏱️ 柔軟なハイブリッド同期トリガー
・定期同期: 5分、10分、15分、30分、60分、120分など指定間隔で自動同期。
・リアルタイム変更検知: ファイルの作成・更新・削除を即座に監視。
・デバウンス制御: 大量コピー中や連続編集時に同期が多重起動しないよう、書き込みが落ち着いてから安全に1回だけ同期を実行。

◆ 🔐 NAS / ネットワーク事前認証 (UNCパス対応)
「\\server\share」などのネットワークパスへのアクセス時、同期直前に自動でセッションを確立。ログインパスワードは Windows DPAPI（Data Protection API）で強力に暗号化され、安全に保管されます。

◆ ⚙️ 高度な Robocopy オプション設定
・完全ミラーリング (/MIR)
・空フォルダを含む全サブフォルダ同期 (/E)
・ファイルロック時のリトライ回数・待機秒数の指定 (/R, /W)
・特定ファイルやフォルダの除外設定 (/XF, /XD)
・任意パラメーターの追加引数指定

◆ 🌓 ダークモード & 多言語完全対応
・Windows のテーマ設定（ライト/ダーク）に完全連動。
・日本語および英語の両表示に対応し、OSの表示言語に合わせて自動切り替え。

◆ 📊 リアルタイムログビューア & ヘルプガイド内蔵
実行履歴や転送結果、エラーをカラーバッジ付きでリアルタイム確認可能。アプリ内には初心者の方にも安心な「操作説明」を完備しています。

※ご注意:
本アプリは Windows 標準の robocopy.exe を使用しています。完全ミラーリング (/MIR) を有効にすると、同期元にないファイルは同期先から自動削除されます。重要なデータでご利用の際は、必ずテスト用フォルダ等で十分検証した上でご利用ください。
```

### 4. 主な機能 (App Features)
*※ 箇条書きで最大20項目（各200文字以内）*
```text
Windows 標準 Robocopy による高速・安全・確実なファイル同期
タスクトレイ常駐による常時バックグラウンド監視と軽量動作
複数の同期フォルダペアを個別に登録・管理・実行可能
リアルタイムファイル変更検知（FileSystemWatcher）とデバウンス制御
5分〜120分のインターバルで指定できる定期タイマー同期
NAS / ネットワーク共有（UNCパス）への自動事前認証機能
Windows DPAPI によるパスワードの安全な暗号化保管
完全ミラーリング (/MIR) や除外設定 (/XF, /XD) などの柔軟なRobocopyオプション
Windows システム連動のダークモード・ライトモード対応
日本語・英語の多言語UI自動切り替え対応
リアルタイム動作ログビューアおよび内蔵操作説明ガイド
```

### 5. 検索キーワード (Search Terms / Keywords)
*※ 最大7個（各30文字以内）*
```text
バックアップ
ファイル同期
フォルダ同期
NAS
Robocopy
自動バックアップ
タスクトレイ
```

### 6. 著作権および商標情報 (Copyright and Trademark Info)
```text
© 2026 Tomokazu Kizawa. All rights reserved.
```

### 7. リリースノート (What's new in this version)
```text
WoodStream FileSync v1.0.1:
- バージョン情報表示およびUI表示の改善
- 最新 .NET 10 ランタイム環境への最適化
- パッケージおよびインストーラの更新

WoodStream FileSync v1.0.0 正式リリース:
- Robocopy ベースのバックグラウンドフォルダ同期機能
- 複数フォルダペアの管理
- リアルタイムファイル変更検知 & 定期インターバル同期
- NAS 自動認証 & DPAPI 暗号化保管
- ダークモード/ライトモードおよび日本語/英語対応
```

---

# 🇺🇸 英語 (English - en-US)

### 1. Product Name
```text
WoodStream FileSync
```

### 2. Short Description (Max 250 characters)
```text
A lightweight Windows system tray tool for automatic folder sync and NAS backup powered by reliable Robocopy. Features real-time change detection, scheduled timers, and NAS authentication.
```

### 3. Description (Max 10,000 characters)
```text
WoodStream FileSync is a reliable background folder synchronization and NAS backup tool built on top of Windows' proven Robocopy engine.

It lives quietly in your system tray, keeping your important work files, photos, and documents securely backed up to your NAS (Network Attached Storage), external hard drive, or local folder.

【🌟 Key Features】

◆ 🚀 System Tray Resident & Lightweight
Runs seamlessly in the Windows notification area with minimal CPU and memory usage. It continues running in the background even when you close the window.

◆ 📁 Multiple Folder Pairs Management
Add and manage multiple source-destination folder pairs. Toggle synchronization on/off, trigger manual sync, or edit individual pairs with ease.

◆ ⏱️ Hybrid Sync Triggers
- Scheduled Sync: Automatically sync files at intervals of 5, 10, 15, 30, 60, or 120 minutes.
- Real-time Change Detection: Immediately detects file creation, modifications, deletions, and renames.
- Debounce Control: Waits until file writes stabilize before running sync, avoiding duplicate runs during large file transfers.

◆ 🔐 NAS & Network Pre-Authentication (UNC Paths)
Automatically establishes a network session right before syncing to UNC network shares (e.g. \\server\share). Passwords are encrypted and safely stored using Windows DPAPI (Data Protection API).

◆ ⚙️ Advanced Robocopy Options
- Full mirroring (/MIR)
- Subdirectory inclusion (/E)
- Retry attempts and wait time (/R, /W)
- Exclude files and directories (/XF, /XD)
- Custom Robocopy arguments support

◆ 🌓 Dark Mode & Multilingual
- Fully integrates with Windows Light and Dark themes.
- Supports both English and Japanese with automatic OS language detection.

◆ 📊 Real-time Log Viewer & Built-in Guide
Inspect file transfer history and status in real-time with color-coded badges. Includes a helpful built-in user guide for beginners.

*Note: This application uses Windows robocopy.exe. When full mirroring (/MIR) is enabled, files in the destination that do not exist in the source will be deleted. Always test with non-critical test folders before production use.
```

### 4. App Features (Bullet Points)
```text
Fast, reliable, and secure file synchronization powered by Windows Robocopy
Lightweight background system tray residency with low resource usage
Manage and execute multiple sync folder pairs independently
Real-time file change monitoring with debounce protection
Scheduled interval sync ranging from 5 to 120 minutes
Automatic network pre-authentication for NAS and UNC shares
Secure credential storage using Windows DPAPI encryption
Customizable Robocopy options including /MIR, /E, /R, /W, /XF, and /XD
Seamless Dark and Light theme integration
Multilingual support (English and Japanese)
Real-time activity log viewer and built-in help guide
```

### 5. Search Terms / Keywords (Max 7 items, 30 chars each)
```text
backup
file sync
folder sync
nas backup
robocopy
auto sync
system tray
```

### 6. Copyright and Trademark Info
```text
© 2026 Tomokazu Kizawa. All rights reserved.
```

### 7. Release Notes (What's new in this version)
```text
WoodStream FileSync v1.0.1:
- UI and version display refinements
- Optimizations for the latest .NET 10 runtime
- Updated installers and packages

Initial release of WoodStream FileSync v1.0.0:
- Robocopy-powered background folder synchronization
- Multiple folder pair management
- Real-time change detection & scheduled interval timers
- NAS pre-authentication with DPAPI credential encryption
- Dark/Light mode and bilingual UI (English/Japanese)
```

---

# ⚙️ システム要件 (System Requirements)

- **OS**: Windows 10 バージョン 1809 (Build 17763.0) 以降、または Windows 11
- **アーキテクチャ**: x64, Arm64
- **メモリ**: 512 MB 以上（推奨 1 GB 以上）
- **ストレージ**: 200 MB 以上の空き容量
- **機能**: キーボード、マウス、タッチ対応

---

# 🔒 プライバシーポリシー & サポート URL 例 (Privacy Policy & Support URLs)

Microsoft Store 申請時に必要となる URL です。GitHub リポジトリのページを指定するのが一般的です：

- **プライバシーポリシー URL**:
  `https://github.com/tkizawa/WoodStreamFileSync#readme`
  または専用の `PRIVACY_POLICY.md` へのリンク
- **サポート URL / お問い合わせ**:
  `https://github.com/tkizawa/WoodStreamFileSync/issues`
