# Privacy Policy / プライバシーポリシー

**Last updated: September 5, 2026**

## English

### 1. Overview
WoodStream FileSync ("the Application") is developed and provided by Tomokazu Kizawa. This privacy policy explains how the Application handles your data.

### 2. Information Collection and Storage
- **No Personal Data Collection**: The Application does not collect, transmit, share, or sell any personal data, usage analytics, or telemetry to the developer or any third parties.
- **Local Configuration and Passwords**: All settings (folder paths, intervals, options) are saved locally on your computer in `%LOCALAPPDATA%\WoodStreamFileSync\config.json`. Any network passwords (e.g. for NAS authentication) are encrypted using Windows DPAPI (`ProtectedData`) and can only be decrypted by your Windows user account on the same machine.
- **Local Log Files**: Activity logs generated during synchronization are saved only on your local computer (`%LOCALAPPDATA%\WoodStreamFileSync\Logs`) and are never sent externally.

### 3. Network Access
The Application only accesses network paths and destinations explicitly configured by you (such as your local network shares or NAS devices via UNC paths) for the sole purpose of file synchronization.

### 4. Contact
If you have any questions or feedback regarding this Privacy Policy, please open an issue at:  
https://github.com/tkizawa/WoodStreamFileSync/issues

---

## 日本語

### 1. 概要
WoodStream FileSync（以下「本アプリ」）は Tomokazu Kizawa が開発・提供しています。本プライバシーポリシーでは、本アプリにおけるデータ等の取り扱いについて説明します。

### 2. 情報の収集および保管について
- **個人情報の非収集**: 本アプリは、利用者の個人情報、利用統計、テレメトリ情報などを収集・外部送信・第三者共有することは一切ありません。
- **設定情報およびパスワードのローカル保管**: すべての設定（同期フォルダパス、同期設定等）はお使いのパソコン内（`%LOCALAPPDATA%\WoodStreamFileSync\config.json`）にローカル保存されます。NAS認証用のパスワードは Windows 標準の暗号化機構 DPAPI（`ProtectedData`）により強力に暗号化され、同一PCの同一Windowsユーザーアカウント以外からは復号できません。
- **ローカルログファイル**: 同期処理のログはお使いのパソコン内（`%LOCALAPPDATA%\WoodStreamFileSync\Logs`）にのみ保存され、外部に送信されることはありません。

### 3. ネットワークアクセス
本アプリによるネットワークアクセスは、利用者が明示的に設定した同期先（NASや社内ネットワーク共有等）に対するファイル同期処理のみを目的として実行されます。

### 4. お問い合わせ
プライバシーポリシーに関するご質問等は、以下の GitHub Issues までお願いいたします：  
https://github.com/tkizawa/WoodStreamFileSync/issues
