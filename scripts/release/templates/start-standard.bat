@echo off
setlocal
chcp 65001 >nul

set "APP_EXE=%~dp0sciman Classroom Toolkit.exe"
set "BOOTSTRAP_PS1=%~dp0bootstrap-runtime.ps1"

if not exist "%APP_EXE%" (
  echo [错误] 未找到程序文件：%APP_EXE%
  pause
  exit /b 10
)

if not exist "%BOOTSTRAP_PS1%" (
  echo [错误] 未找到运行时引导脚本：%BOOTSTRAP_PS1%
  echo 请重新解压标准版压缩包后重试。
  pause
  exit /b 11
)

powershell -NoProfile -File "%BOOTSTRAP_PS1%"
if errorlevel 1 (
  echo.
  echo [提示] 运行环境准备失败。
  echo 1. 请先安装 .NET Desktop Runtime 10.x；
  echo    官方下载： https://dotnet.microsoft.com/download/dotnet/10.0
  echo 2. 若当前电脑策略禁止安装运行时，请改用离线版。
  pause
  exit /b 12
)

start "" "%APP_EXE%"
exit /b 0
