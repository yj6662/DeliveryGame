@echo off
echo =========================================
echo   Unity Access Token Warmup Starting...
echo =========================================

set UNITY_EXE="C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe"

%UNITY_EXE% ^
 -quit -batchmode ^
 -username "%UNITY_EMAIL%" ^
 -password "%UNITY_PASSWORD%" ^
 -logfile "C:\Temp\unity_token_warmup.log"

if %ERRORLEVEL% neq 0 (
    echo.
    echo ❌ Unity token warmup failed. Check log:
    echo    C:\Temp\unity_token_warmup.log
    exit /b 1
)

echo.
echo ✅ Unity token warmup completed successfully.
exit /b 0