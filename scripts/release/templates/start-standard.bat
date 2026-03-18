@echo off
setlocal
chcp 65001 >nul

set "APP_EXE=%~dp0ClassroomToolkit.App.exe"
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
  echo 1. 请先运行 prereq 目录中的 .NET Desktop Runtime 安装包；
  echo 2. 若当前电脑策略禁止安装运行时，请改用离线版。
  pause
  exit /b 12
)

start "" "%APP_EXE%"
exit /b 0
