# 更新已安装的 GamepadMouse（修复/升级安装目录）
# 用法：.\scripts\update-installed.ps1 [-InstallDir "D:\software\GamepadMouse"]
#
# 注意：安装目录里的 exe 必须用"自包含单文件发布"产物覆盖
# （bin\Release 下的开发构建只是启动器，单独复制无法运行！）
param(
    [string]$InstallDir = "D:\software\GamepadMouse"
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot

$env:DOTNET_ROOT = "D:\software\dotnet-sdk"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"

# 结束运行中的实例
Get-Process GamepadMouse -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

& "D:\software\dotnet-sdk\dotnet.exe" publish (Join-Path $repo "src\GamepadMouse") `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o (Join-Path $repo "dist\publish")

Copy-Item (Join-Path $repo "dist\publish\GamepadMouse.exe") (Join-Path $InstallDir "GamepadMouse.exe") -Force
Write-Host "已更新 $InstallDir\GamepadMouse.exe"

# 重新启动
Start-Process (Join-Path $InstallDir "GamepadMouse.exe")
Write-Host "已启动 GamepadMouse"
