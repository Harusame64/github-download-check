# GitHub Download Check - ディストリビューションビルドスクリプト
# 使用方法: powershell -ExecutionPolicy Bypass -File build-dist.ps1 [-Version "1.0.0"]

param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
$ProjectDir = Join-Path $Root "src\GitHubDownloadCheck"
$PublishDir = Join-Path $ProjectDir "bin\Release\net10.0\win-x64\publish"
$InstallerDir = Join-Path $Root "installer"
$DistDir = Join-Path $Root "dist"

Write-Host "=== GitHub Download Check Build ===" -ForegroundColor Cyan
Write-Host "Version: $Version"
Write-Host ""

# 1. Release ビルド & Publish
Write-Host "[1/3] Building & publishing..." -ForegroundColor Yellow
dotnet publish "$ProjectDir\GitHubDownloadCheck.csproj" -c Release | Out-Host
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
Write-Host "  -> $PublishDir" -ForegroundColor Green

# 2. MSI インストーラ生成 (バージョンは環境変数で渡す)
Write-Host "[2/3] Building MSI installer..." -ForegroundColor Yellow
$MsiOut = Join-Path $InstallerDir "GitHubDownloadCheck-$Version.msi"
$env:PRODUCT_VERSION = $Version

Push-Location $InstallerDir
try {
    wix build GitHubDownloadCheck.wxs -ext WixToolset.UI.wixext -o "GitHubDownloadCheck-$Version.msi"
    if ($LASTEXITCODE -ne 0) { throw "wix build failed" }
} finally {
    $env:PRODUCT_VERSION = $null
    Pop-Location
}
Write-Host "  -> $MsiOut" -ForegroundColor Green

# 3. Zipアーカイブ生成
Write-Host "[3/3] Creating zip archive..." -ForegroundColor Yellow
$null = New-Item -ItemType Directory -Force -Path $DistDir
$ZipFile = Join-Path $DistDir "GitHubDownloadCheck-$Version-win-x64.zip"
$ExeFile = Join-Path $PublishDir "GitHubDownloadCheck.exe"
Compress-Archive -Path $ExeFile -DestinationPath $ZipFile -Force
Write-Host "  -> $ZipFile" -ForegroundColor Green

# dist フォルダにMSIもコピー
Copy-Item $MsiOut $DistDir -Force
Write-Host "  -> MSI copied to dist\" -ForegroundColor Green

Write-Host ""
Write-Host "=== Build completed ===" -ForegroundColor Cyan
Write-Host ""
Get-ChildItem $DistDir | Format-Table Name, @{L="Size";E={"{0:N1} MB" -f ($_.Length/1MB)}}
