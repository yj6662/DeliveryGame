@echo off
setlocal

set "UNITY_EXE=C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe"
set "PROJECT_PATH=C:\Users\yj666\DeliveryGame\DeliveryGame"
set "RESULT_PATH=C:\Users\yj666\DeliveryGame\TestResults_TrafficNpc_EditMode.xml"
set "LOG_PATH=C:\Users\yj666\DeliveryGame\Batch_EditMode_TrafficNpc.log"

echo [Validate_EditMode] Start: %DATE% %TIME%
"%UNITY_EXE%" ^
 -batchmode -nographics ^
 -projectPath "%PROJECT_PATH%" ^
 -runTests ^
 -testPlatform EditMode ^
 -runSynchronously ^
 -testFilter "DeliveryRun.Tests.EditMode.TrafficNpcRoadRuleEditModeTest" ^
 -testResults "%RESULT_PATH%" ^
 -logFile "%LOG_PATH%"

set "EXIT_CODE=%ERRORLEVEL%"
echo [Validate_EditMode] Unity ExitCode=%EXIT_CODE%
if exist "%RESULT_PATH%" (
  echo [Validate_EditMode] TestResults: %RESULT_PATH%
) else (
  echo [Validate_EditMode] TestResults not found.
)
echo [Validate_EditMode] End: %DATE% %TIME%
exit /b %EXIT_CODE%
