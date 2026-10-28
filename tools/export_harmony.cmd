@echo off
chcp 65001 >nul
REM ===========================================================================
REM  Unity -> OpenHarmony export + build pipeline launcher (double-click to run)
REM
REM  Usage:
REM    export_harmony.cmd                    full auto (launches Unity CLI export)
REM    export_harmony.cmd -SkipUnityExport   Unity already open: post-process + build only
REM    export_harmony.cmd -SkipBuild         export only, skip packaging
REM ===========================================================================
setlocal
set "SCRIPT_DIR=%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%export_harmony.ps1" %*
set "RC=%ERRORLEVEL%"
echo.
if "%RC%"=="0" (
  echo [DONE] All steps completed.
) else (
  echo [FAILED] Exit code %RC% - check the log above.
)
pause
exit /b %RC%
