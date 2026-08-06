# Desktop Peek — 开发运行脚本（不打包，直接 dotnet run）
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Get-Process -Name "DesktopPeek" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 300

Write-Host "Building & starting Desktop Peek (dotnet run)..." -ForegroundColor Cyan
dotnet run -c Debug --no-launch-profile
