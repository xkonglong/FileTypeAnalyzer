@echo off
chcp 65001 >nul
setlocal
pushd "%~dp0"

set "PROJ=FileTypeAnalyzer.csproj"
set "CONFIG=Debug"
if /i "%~1"=="release" set "CONFIG=Release"
if /i "%~1"=="r"       set "CONFIG=Release"
if /i "%~1"=="debug"   set "CONFIG=Debug"

echo ==========================================
echo   FileTypeAnalyzer 编译脚本
echo ==========================================
echo   项目 : %PROJ%
echo   配置 : %CONFIG%
echo.

if not exist "%PROJ%" (
    echo [错误] 当前目录下找不到 %PROJ%
    echo        请把本脚本放在项目根目录（与 csproj 同级）运行。
    goto :fail
)

rem ---- 定位 dotnet ----
set "DOTNET="
where dotnet >nul 2>nul
if not errorlevel 1 set "DOTNET=dotnet"
if not defined DOTNET if exist "%ProgramFiles%\dotnet\dotnet.exe" set "DOTNET=%ProgramFiles%\dotnet\dotnet.exe"
if not defined DOTNET if exist "%ProgramFiles(x86)%\dotnet\dotnet.exe" set "DOTNET=%ProgramFiles(x86)%\dotnet\dotnet.exe"
if not defined DOTNET (
    echo [错误] 未找到 dotnet 命令，请先安装 .NET 8 SDK。
    goto :fail
)
echo   编译器: %DOTNET%
echo.

rem ---- 编译 ----
"%DOTNET%" build "%PROJ%" -c %CONFIG% -v minimal
if errorlevel 1 goto :fail

set "OUTDIR=bin\%CONFIG%\net8.0-windows"
echo.
echo [成功] 输出目录: %~dp0%OUTDIR%
echo.

rem ---- 运行时依赖提示（只检查，不影响编译结果）----
for %%F in (TrIDLib.dll triddefs.trd magika.exe) do (
    if not exist "%OUTDIR%\%%F" echo [提示] 输出目录缺少 %%F，运行时请放入该文件。
)
if not exist "%OUTDIR%\lang\*.json" echo [提示] 输出目录缺少 lang\*.json，界面将使用内置默认文案。

echo.
echo 提示：直接传参 release 可编译 Release 版，例如  编译.bat release
echo.
pause
popd
exit /b 0

:fail
echo.
echo [失败] 编译未通过，请检查上面的错误信息。
echo.
pause
popd
exit /b 1