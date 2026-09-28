# GamepadMouse 构建脚本
# 用法：
#   .\scripts\build.ps1              # 普通构建（依赖本机 .NET 8 桌面运行时）
#   .\scripts\build.ps1 -Publish     # 发布自包含单文件 exe（无需安装运行时）
param(
    [switch]$Publish
)

$ErrorActionPreference = "Stop"

# 本机安装的 SDK 位置（安装到 D 盘，避免占用 C 盘）
$dotnet = "D:\software\dotnet-sdk\dotnet.exe"
if (-not (Test-Path $dotnet)) { $dotnet = "dotnet" }

$env:DOTNET_ROOT = "D:\software\dotnet-sdk"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"

$proj = Join-Path $PSScriptRoot "..\src\GamepadMouse\GamepadMouse.csproj"

if ($Publish) {
    & $dotnet publish $proj -c Release -r win-x64 --self-contained `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
    Write-Host ""
    Write-Host "发布完成：src\GamepadMouse\bin\Release\net8.0-windows\win-x64\publish\GamepadMouse.exe"
} else {
    & $dotnet build $proj -c Release
    Write-Host ""
    Write-Host "构建完成：src\GamepadMouse\bin\Release\net8.0-windows\GamepadMouse.exe（需要 .NET 8 桌面运行时）"
}
