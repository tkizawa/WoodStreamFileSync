# WoodStreamFileSync - Microsoft Store 向け MSIX パッケージ自動ビルドスクリプト
# プロジェクトルール:
# - Microsoft Store に登録する場合は .\MSIX フォルダに msix ファイルを作成すること。
# - アーキテクチャは x64 と Arm64 を作成すること。
# - MSIX フォルダ（.\MSIX）は Git のコミット対象外（.gitignore に追加）とすること。

param(
    [string]$Configuration = "Release",
    [switch]$CreateBundle = $true,
    [switch]$SignPackage = $false
)

$ErrorActionPreference = "Stop"

Write-Host "=================================================" -ForegroundColor Cyan
Write-Host " WoodStreamFileSync Microsoft Store MSIX ビルド " -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan

# 1. バージョン番号の抽出 (WoodStreamFileSync.csproj より)
$csprojPath = Join-Path $PSScriptRoot "WoodStreamFileSync.csproj"
if (!(Test-Path $csprojPath)) {
    throw "WoodStreamFileSync.csproj が見つかりません: $csprojPath"
}

[xml]$projXml = Get-Content $csprojPath
$version = $projXml.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) {
    $version = "1.0.0.0"
}
Write-Host "検出バージョン: $version" -ForegroundColor Green

# 2. 出力フォルダの作成
$msixDir = Join-Path $PSScriptRoot "MSIX"
if (!(Test-Path $msixDir)) {
    New-Item -ItemType Directory -Path $msixDir | Out-Null
}

# 3. makeappx.exe の探索
$makeappx = (Get-ChildItem "$env:USERPROFILE\.nuget\packages\microsoft.windows.sdk.buildtools" -Filter "makeappx.exe" -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -like "*\x64\makeappx.exe" } |
    Sort-Object FullName -Descending |
    Select-Object -First 1).FullName

if (!$makeappx) {
    # Windows SDK インストールパスを検索
    $sdkCandidates = @(
        "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\makeappx.exe",
        "C:\Program Files\Windows Kits\10\bin\*\x64\makeappx.exe"
    )
    foreach ($pat in $sdkCandidates) {
        $found = Get-Item $pat -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1
        if ($found) {
            $makeappx = $found.FullName
            break
        }
    }
}

if (!$makeappx) {
    throw "makeappx.exe が見つかりませんでした。Microsoft.Windows.SDK.BuildTools を確認してください。"
}
Write-Host "makeappx パス: $makeappx" -ForegroundColor Green

# 4. パッケージアセットの確認・生成
$assetsDir = Join-Path $PSScriptRoot "PackageAssets"
$requiredAssets = @("Square44x44Logo.png", "Square150x150Logo.png", "Wide310x150Logo.png", "StoreLogo.png", "SplashScreen.png")
$missingAssets = $requiredAssets | Where-Object { !(Test-Path (Join-Path $assetsDir $_)) }

if ($missingAssets.Count -gt 0) {
    Write-Host "アセット画像を生成中..." -ForegroundColor Yellow
    Add-Type -AssemblyName System.Drawing
    $srcPath = Join-Path $PSScriptRoot "Resources\app_icon.png"
    if (!(Test-Path $assetsDir)) { New-Item -ItemType Directory -Path $assetsDir | Out-Null }
    $src = [System.Drawing.Image]::FromFile($srcPath)

    function Local-Resize-Canvas($w, $h, $destName) {
        $bmp = New-Object System.Drawing.Bitmap($w, $h)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.Clear([System.Drawing.Color]::Transparent)

        $scale = [Math]::Min($w / $src.Width, $h / $src.Height)
        $drawW = [int]($src.Width * $scale)
        $drawH = [int]($src.Height * $scale)
        $drawX = [int](($w - $drawW) / 2)
        $drawY = [int](($h - $drawH) / 2)

        $g.DrawImage($src, $drawX, $drawY, $drawW, $drawH)
        $g.Dispose()

        $destPath = Join-Path $assetsDir $destName
        $bmp.Save($destPath, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
    }

    Local-Resize-Canvas 44 44 "Square44x44Logo.png"
    Local-Resize-Canvas 24 24 "Square44x44Logo.targetsize-24_altform-unplated.png"
    Local-Resize-Canvas 150 150 "Square150x150Logo.png"
    Local-Resize-Canvas 310 150 "Wide310x150Logo.png"
    Local-Resize-Canvas 50 50 "StoreLogo.png"
    Local-Resize-Canvas 620 300 "SplashScreen.png"
    $src.Dispose()
    Write-Host "アセット画像生成完了。" -ForegroundColor Green
}

# 5. マニフェストテンプレートの読み込み
$manifestTemplatePath = Join-Path $PSScriptRoot "Package.appxmanifest.template.xml"
if (!(Test-Path $manifestTemplatePath)) {
    throw "マニフェストテンプレートが見つかりません: $manifestTemplatePath"
}
$manifestTemplate = Get-Content $manifestTemplatePath -Raw -Encoding UTF8

# 6. 対象アーキテクチャのビルド (x64, arm64)
$architectures = @("x64", "arm64")
$stagingRoot = Join-Path $PSScriptRoot "msix_staging"
$bundleStageDir = Join-Path $stagingRoot "bundle_input"
if (Test-Path $bundleStageDir) { Remove-Item -Path $bundleStageDir -Recurse -Force }
New-Item -ItemType Directory -Path $bundleStageDir | Out-Null

$createdPackages = @()

foreach ($arch in $architectures) {
    Write-Host "-------------------------------------------------" -ForegroundColor Cyan
    Write-Host " アーキテクチャ: $arch のビルド開始 " -ForegroundColor Yellow
    Write-Host "-------------------------------------------------" -ForegroundColor Cyan

    $publishDir = Join-Path $PSScriptRoot "publish\win-$arch"
    Write-Host "発行中 (dotnet publish -r win-$arch)..." -ForegroundColor Yellow
    dotnet publish $csprojPath -c $Configuration -r "win-$arch" --self-contained true -o $publishDir
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish (win-$arch) に失敗しました。"
    }

    # ステージングフォルダ作成
    $stageDir = Join-Path $stagingRoot $arch
    if (Test-Path $stageDir) { Remove-Item -Path $stageDir -Recurse -Force }
    New-Item -ItemType Directory -Path $stageDir | Out-Null

    # 発行バイナリをコピー
    Copy-Item -Path "$publishDir\*" -Destination $stageDir -Recurse -Force

    # PackageAssets をコピー
    $stageAssets = Join-Path $stageDir "PackageAssets"
    New-Item -ItemType Directory -Path $stageAssets -Force | Out-Null
    Copy-Item -Path "$assetsDir\*" -Destination $stageAssets -Recurse -Force

    # AppxManifest.xml を生成
    $manifestContent = $manifestTemplate.Replace("{{VERSION}}", $version).Replace("{{ARCH}}", $arch)
    $manifestDest = Join-Path $stageDir "AppxManifest.xml"
    Set-Content -Path $manifestDest -Value $manifestContent -Encoding UTF8

    # makeappx pack 実行
    $msixName = "WoodStreamFileSync_v${version}_${arch}.msix"
    $msixPath = Join-Path $msixDir $msixName
    Write-Host "MSIX パッケージ生成中: $msixName ..." -ForegroundColor Yellow
    & $makeappx pack /d $stageDir /p $msixPath /o
    if ($LASTEXITCODE -ne 0) {
        throw "makeappx pack ($arch) に失敗しました。"
    }

    $createdPackages += $msixPath
    # バンドル用にコピー
    Copy-Item -Path $msixPath -Destination (Join-Path $bundleStageDir $msixName) -Force

    Write-Host "生成完了: $msixPath" -ForegroundColor Green
}

# 7. MSIX バンドル (.msixbundle) の作成
if ($CreateBundle) {
    Write-Host "-------------------------------------------------" -ForegroundColor Cyan
    Write-Host " 統合 MSIX バンドルの作成 " -ForegroundColor Yellow
    Write-Host "-------------------------------------------------" -ForegroundColor Cyan

    $bundleName = "WoodStreamFileSync_v${version}.msixbundle"
    $bundlePath = Join-Path $msixDir $bundleName
    & $makeappx bundle /d $bundleStageDir /p $bundlePath /bv $version /o
    if ($LASTEXITCODE -ne 0) {
        throw "makeappx bundle に失敗しました。"
    }
    Write-Host "生成完了: $bundlePath" -ForegroundColor Green
}

# 一時ステージングディレクトリのクリーンアップ
if (Test-Path $stagingRoot) {
    Remove-Item -Path $stagingRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "=================================================" -ForegroundColor Cyan
Write-Host " Microsoft Store 用 MSIX パッケージ生成完了! " -ForegroundColor Green
Write-Host " 出力ディレクトリ: $msixDir" -ForegroundColor Green
Get-ChildItem -Path $msixDir | Format-Table Name, Length, LastWriteTime
Write-Host "=================================================" -ForegroundColor Cyan
