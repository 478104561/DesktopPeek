# Desktop Peek — 单文件发布（输出带版本号）
# 用法:
#   .\publish.ps1
#   .\publish.ps1 -Version 1.2.0    # 写入 csproj 并按该版本打包
param(
    [string]$Version
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$csproj = Join-Path $PSScriptRoot "DesktopPeek.csproj"
if (-not (Test-Path $csproj)) {
    throw "DesktopPeek.csproj not found."
}

function Get-ProjectVersion([string]$path) {
    [xml]$xml = Get-Content -Raw $path
    $node = $xml.Project.PropertyGroup.Version | Select-Object -First 1
    if (-not $node) { throw "Version not found in $path" }
    return "$node".Trim()
}

function Set-ProjectVersion([string]$path, [string]$ver) {
    if ($ver -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') {
        throw "Invalid version '$ver'. Use like 1.2.0"
    }
    $content = Get-Content -Raw $path
    if ($content -notmatch '<Version>[^<]+</Version>') {
        throw "Could not locate <Version> in $path"
    }
    $updated = [regex]::Replace($content, '<Version>[^<]+</Version>', "<Version>$ver</Version>")
    Set-Content -Path $path -Value $updated -NoNewline
}

if ($Version) {
    Write-Host "Updating project Version to $Version ..." -ForegroundColor Cyan
    Set-ProjectVersion $csproj $Version
}

$ver = Get-ProjectVersion $csproj
Write-Host "Publishing Desktop Peek v$ver (win-x64 single-file)..." -ForegroundColor Cyan

Get-Process -Name "DesktopPeek" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 300

$outDir = Join-Path $PSScriptRoot "publish"
dotnet publish -c Release -r win-x64 `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:Version=$ver `
    -p:AssemblyVersion="$ver.0" `
    -p:FileVersion="$ver.0" `
    -p:InformationalVersion=$ver `
    --self-contained true `
    -o $outDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

$exe = Join-Path $outDir "DesktopPeek.exe"
$versioned = Join-Path $outDir "DesktopPeek-v$ver.exe"
if (-not (Test-Path $exe)) {
    throw "Publish succeeded but DesktopPeek.exe was not found."
}

Copy-Item -Path $exe -Destination $versioned -Force

# Remove pdb from distribute folder noise (optional keep for local debug)
$pdb = Join-Path $outDir "DesktopPeek.pdb"
if (Test-Path $pdb) {
    Remove-Item $pdb -Force
}

$sizeMb = [math]::Round((Get-Item $versioned).Length / 1MB, 2)
Write-Host ""
Write-Host "Done." -ForegroundColor Green
Write-Host "  Version : $ver"
Write-Host "  EXE     : $versioned"
Write-Host "  Also    : $exe"
Write-Host "  Size    : $sizeMb MB"
