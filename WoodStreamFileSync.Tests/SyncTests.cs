using System;
using System.IO;
using System.Threading.Tasks;
using WoodStreamFileSync.Models;
using WoodStreamFileSync.Services;
using Xunit;

namespace WoodStreamFileSync.Tests;

/// <summary>
/// 同期エンジン、認証ユーティリティ、暗号化、フォルダ監視、設定モデルの単体テストクラス
/// </summary>
public class SyncTests
{
    /// <summary>
    /// アプリケーションアイコンファイル（ico / png）が存在するかテスト
    /// </summary>
    [Fact]
    public void Test_App_Icon_Exists()
    {
        var destIco = @"c:\Dev\WoodStreamFileSync\Resources\app_icon.ico";
        var destPng = @"c:\Dev\WoodStreamFileSync\Resources\app_icon.png";

        Assert.True(File.Exists(destIco));
        Assert.True(File.Exists(destPng));
    }

    /// <summary>
    /// Windows DPAPI による平文パスワードの暗号化および復号が正常に行われるかテスト
    /// </summary>
    [Fact]
    public void Test_DPAPI_Encryption_Decryption()
    {
        var original = "SecretP@ssw0rd!2026";
        var encrypted = ConfigManager.EncryptPassword(original);

        Assert.NotEmpty(encrypted);
        Assert.NotEqual(original, encrypted);

        var decrypted = ConfigManager.DecryptPassword(encrypted);
        Assert.Equal(original, decrypted);
    }

    /// <summary>
    /// UNC パスの判定ロジックおよび共有ルート抽出（\\server\share）が正しく動作するかテスト
    /// </summary>
    [Fact]
    public void Test_UncPath_Detection_And_RootExtraction()
    {
        Assert.True(NasAuthenticator.IsUncPath(@"\\192.168.1.100\share"));
        Assert.True(NasAuthenticator.IsUncPath(@"\\synology-nas\backup\folder1\sub"));
        Assert.False(NasAuthenticator.IsUncPath(@"C:\Local\Folder"));
        Assert.False(NasAuthenticator.IsUncPath(""));

        Assert.Equal(@"\\192.168.1.100\share", NasAuthenticator.ExtractUncShareRoot(@"\\192.168.1.100\share\folder\sub"));
        Assert.Equal(@"\\nas01\backup", NasAuthenticator.ExtractUncShareRoot(@"\\nas01\backup"));
    }

    /// <summary>
    /// Robocopy のビットマスク終了コードに応じた成否評価およびメッセージ判定テスト
    /// </summary>
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(7, true)]
    [InlineData(8, false)]
    [InlineData(16, false)]
    public void Test_Robocopy_ExitCode_Evaluation(int exitCode, bool expectedSuccess)
    {
        var (success, description) = RobocopyRunner.EvaluateExitCode(exitCode);
        Assert.Equal(expectedSuccess, success);
        Assert.NotEmpty(description);
    }

    /// <summary>
    /// ローカル一時フォルダ間での Robocopy 実行・ファイルコピー動作テスト
    /// </summary>
    [Fact]
    public async Task Test_Local_Folder_Robocopy_Execution()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "WoodStreamSyncTest_" + Guid.NewGuid().ToString("N"));
        var src = Path.Combine(tempRoot, "src");
        var dest = Path.Combine(tempRoot, "dest");

        try
        {
            Directory.CreateDirectory(src);
            Directory.CreateDirectory(dest);

            var testFile1 = Path.Combine(src, "test1.txt");
            var testSubDir = Path.Combine(src, "sub");
            Directory.CreateDirectory(testSubDir);
            var testFile2 = Path.Combine(testSubDir, "test2.txt");

            File.WriteAllText(testFile1, "Hello Sync 1");
            File.WriteAllText(testFile2, "Hello Sync 2");

            var runner = new RobocopyRunner();
            var options = new RobocopyOptions
            {
                IsMirror = true,
                RetryCount = 1,
                WaitTimeSeconds = 1
            };

            var result = await runner.RunAsync(src, dest, options);

            Assert.True(result.Success);
            Assert.True(File.Exists(Path.Combine(dest, "test1.txt")));
            Assert.True(File.Exists(Path.Combine(dest, "sub", "test2.txt")));
            Assert.Equal("Hello Sync 1", File.ReadAllText(Path.Combine(dest, "test1.txt")));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                try { Directory.Delete(tempRoot, true); } catch { }
            }
        }
    }

    /// <summary>
    /// フォルダ監視サービスで短時間に複数回発生した変更が1回の同期イベントに集約（デバウンス）されるかテスト
    /// </summary>
    [Fact]
    public async Task Test_FolderWatcher_Debounce()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "WoodStreamWatcherTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        using var watcher = new FolderWatcherService();
        int triggerCount = 0;
        watcher.ChangeDetectedAndSettled += () =>
        {
            Interlocked.Increment(ref triggerCount);
        };

        try
        {
            // 1秒のデバウンスで開始
            watcher.Start(tempDir, 1);

            // 連続して3回ファイル書き込み（0.2秒間隔）
            for (int i = 0; i < 3; i++)
            {
                File.WriteAllText(Path.Combine(tempDir, $"file_{i}.txt"), $"Content {i}");
                await Task.Delay(200);
            }

            // デバウンス待機完了を待つ (1.5秒)
            await Task.Delay(1500);

            // 3回の連続変更に対して、トリガーは1回のみ集約されていること
            Assert.Equal(1, triggerCount);
        }
        finally
        {
            watcher.Stop();
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    /// <summary>
    /// SyncFolderPair の DisplayName プロパティが設定名称やパスに応じて適切に解決されるかテスト
    /// </summary>
    [Fact]
    public void Test_SyncFolderPair_DisplayName()
    {
        var pair1 = new SyncFolderPair { Name = "MyBackup", SourcePath = @"C:\Folder1" };
        Assert.Equal("MyBackup", pair1.DisplayName);

        var pair2 = new SyncFolderPair { Name = "", SourcePath = @"C:\Folder1\SubFolder" };
        Assert.Equal("SubFolder", pair2.DisplayName);

        var pair3 = new SyncFolderPair { Name = "", SourcePath = "" };
        Assert.Equal("新規フォルダペア", pair3.DisplayName);
    }

    /// <summary>
    /// 複数フォルダペアを設定した SyncManager の一括同期実行テスト
    /// </summary>
    [Fact]
    public async Task Test_Multiple_Folder_Sync_Execution()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "WoodStreamMultiSync_" + Guid.NewGuid().ToString("N"));
        var src1 = Path.Combine(tempRoot, "src1");
        var dest1 = Path.Combine(tempRoot, "dest1");
        var src2 = Path.Combine(tempRoot, "src2");
        var dest2 = Path.Combine(tempRoot, "dest2");

        try
        {
            Directory.CreateDirectory(src1);
            Directory.CreateDirectory(dest1);
            Directory.CreateDirectory(src2);
            Directory.CreateDirectory(dest2);

            File.WriteAllText(Path.Combine(src1, "pair1.txt"), "Content Pair 1");
            File.WriteAllText(Path.Combine(src2, "pair2.txt"), "Content Pair 2");

            var config = new AppConfig
            {
                EnableRealtimeSync = false,
                EnablePeriodicSync = false,
                FolderPairs = new System.Collections.Generic.List<SyncFolderPair>
                {
                    new SyncFolderPair { Name = "Pair 1", SourcePath = src1, DestinationPath = dest1, IsEnabled = true },
                    new SyncFolderPair { Name = "Pair 2", SourcePath = src2, DestinationPath = dest2, IsEnabled = true }
                }
            };

            var syncManager = new SyncManager();
            syncManager.Initialize(config);

            var result = await syncManager.ExecuteSyncAsync("UnitTest");

            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.True(File.Exists(Path.Combine(dest1, "pair1.txt")));
            Assert.True(File.Exists(Path.Combine(dest2, "pair2.txt")));
            Assert.Equal("Content Pair 1", File.ReadAllText(Path.Combine(dest1, "pair1.txt")));
            Assert.Equal("Content Pair 2", File.ReadAllText(Path.Combine(dest2, "pair2.txt")));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                try { Directory.Delete(tempRoot, true); } catch { }
            }
        }
    }
}

