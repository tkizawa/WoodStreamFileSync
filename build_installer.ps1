# WoodStreamFileSync - インストーラ自動ビルドスクリプト
# プロジェクトルール:
# - スタンドアロンインストーラは exe 形式で .\Installer フォルダに作成し、ファイル名にはバージョン番号を含めること。
# - 実行環境のアーキテクチャ（x64、Arm64 等）に合わせたものを作成すること。
# - Installer フォルダ（.\Installer）は Git のコミット対象外（.gitignore に追加）とすること。

param(
    [string]$Configuration = "Release",
    [string[]]$Architectures = @("x64", "arm64")
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " WoodStreamFileSync インストーラ ビルド " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

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
Write-Host "対象アーキテクチャ: $($Architectures -join ', ')" -ForegroundColor Green

# 2. Installer 出力フォルダの確認
$installerDir = Join-Path $PSScriptRoot "Installer"
if (!(Test-Path $installerDir)) {
    New-Item -ItemType Directory -Path $installerDir | Out-Null
}

# 3. Inno Setup (ISCC.exe) の検索
$isccCandidates = @(
    "C:\Users\$env:USERNAME\AppData\Local\Programs\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
)

$isccPath = $null
foreach ($path in $isccCandidates) {
    if (Test-Path $path) {
        $isccPath = $path
        break
    }
}

if (!$isccPath) {
    $cmd = Get-Command iscc.exe -ErrorAction SilentlyContinue
    if ($cmd) {
        $isccPath = $cmd.Source
    }
}

if (!$isccPath) {
    throw "Inno Setup Compiler (ISCC.exe) が見つかりませんでした。"
}

Write-Host "ISCC パス: $isccPath" -ForegroundColor Green
$issPath = Join-Path $PSScriptRoot "installer.iss"

# 4. 各アーキテクチャのビルドおよびインストーラ作成
foreach ($arch in $Architectures) {
    Write-Host "----------------------------------------" -ForegroundColor Cyan
    Write-Host " アーキテクチャ: $arch のビルド開始 " -ForegroundColor Yellow
    Write-Host "----------------------------------------" -ForegroundColor Cyan

    $publishDir = Join-Path $PSScriptRoot "publish\win-$arch"
    Write-Host "発行中 (dotnet publish -r win-$arch)..." -ForegroundColor Yellow
    dotnet publish $csprojPath -c $Configuration -r "win-$arch" --self-contained true -o $publishDir
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish (win-$arch) に失敗しました。"
    }

    $outputBaseName = "WoodStreamFileSync_v${version}_${arch}_Setup"
    Write-Host "インストーラをコンパイル中: $outputBaseName.exe ..." -ForegroundColor Yellow

    & $isccPath /DMyAppVersion="$version" /DMyAppArch="$arch" /DMyOutputDir="$installerDir" /DMySourceDir="$publishDir" $issPath
    if ($LASTEXITCODE -ne 0) {
        throw "ISCC コンパイル ($arch) に失敗しました。"
    }

    $outputExe = Join-Path $installerDir "$outputBaseName.exe"
    Write-Host "作成完了: $outputExe" -ForegroundColor Green
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " 全インストーラ作成完了! " -ForegroundColor Green
Write-Host " 出力ディレクトリ: $installerDir" -ForegroundColor Green
Get-ChildItem -Path $installerDir -Filter "*.exe" | Format-Table Name, Length, LastWriteTime
Write-Host "========================================" -ForegroundColor Cyan