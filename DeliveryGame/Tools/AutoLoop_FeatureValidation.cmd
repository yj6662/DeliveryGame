@echo off
setlocal EnableDelayedExpansion

set "MAX_ITERS=%~1"
if "%MAX_ITERS%"=="" set "MAX_ITERS=0"

set /a ITER=0

echo [AutoLoop] Start: %DATE% %TIME%
echo [AutoLoop] MAX_ITERS=%MAX_ITERS% (0 = infinite)

:LOOP
set /a ITER+=1
echo [AutoLoop] Iteration !ITER! start: %DATE% %TIME%

call "%~dp0RunTests_TrafficNpc_PlayMode.cmd"
if errorlevel 1 goto FAIL

call "%~dp0Validate_TrafficNpc_EditMode.cmd"
if errorlevel 1 goto FAIL

echo [AutoLoop] Iteration !ITER! PASS

if "%MAX_ITERS%"=="0" goto CONTINUE
if !ITER! GEQ %MAX_ITERS% goto DONE

:CONTINUE
timeout /t 2 /nobreak >nul
goto LOOP

:FAIL
echo [AutoLoop] FAILED at iteration !ITER!
exit /b 1

:DONE
echo [AutoLoop] Completed !ITER! iterations.
exit /b 0
