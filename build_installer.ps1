# WoodStreamFileSync - インストーラ自動ビルドスクリプト
# プロジェクトルール:
# - スタンドアロンインストーラは exe 形式で .\Installer フォルダに作成し、ファイル名にはバージョン番号を含めること。
# - 実行環境のアーキテクチャ（x64 等）に合わせたものを作成すること。

param(
    [string]$Configuration = "Release",
    [string]$Architecture = "x64"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " WoodStreamFileSync インストーラ ビルド開始 " -ForegroundColor Cyan
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
Write-Host "ターゲットアーキテクチャ: $Architecture" -ForegroundColor Green

# 2. Installer 出力フォルダの確認
$installerDir = Join-Path $PSScriptRoot "Installer"
if (!(Test-Path $installerDir)) {
    New-Item -ItemType Directory -Path $installerDir | Out-Null
}

# 3. dotnet publish (自己完結型) の実行
$publishDir = Join-Path $PSScriptRoot "publish\win-$Architecture"
Write-Host "発行中 (dotnet publish)..." -ForegroundColor Yellow
dotnet publish $csprojPath -c $Configuration -r "win-$Architecture" --self-contained true -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish に失敗しました。"
}

# 4. Inno Setup (ISCC.exe) の検索
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

# 5. Inno Setup コンパイル実行
$issPath = Join-Path $PSScriptRoot "installer.iss"
$outputBaseName = "WoodStreamFileSync_v${version}_${Architecture}_Setup"

Write-Host "インストーラをコンパイル中..." -ForegroundColor Yellow
& $isccPath /DMyAppVersion="$version" /DMyOutputDir="$installerDir" /DMyOutputBaseFilename="$outputBaseName" $issPath
if ($LASTEXITCODE -ne 0) {
    throw "ISCC コンパイルに失敗しました。"
}

$outputExe = Join-Path $installerDir "$outputBaseName.exe"
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " インストーラ作成完了! " -ForegroundColor Green
Write-Host " 出力ファイル: $outputExe" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
