@echo off
setlocal

set "UNITY_EXE=C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe"
set "PROJECT_PATH=C:\Users\yj666\DeliveryGame\DeliveryGame"
set "RESULT_PATH=C:\Users\yj666\DeliveryGame\TestResults_TrafficNpc.xml"
set "LOG_PATH=C:\Users\yj666\DeliveryGame\Batch_PlayMode_TrafficNpc.log"

echo [RunTests_PlayMode] Start: %DATE% %TIME%
"%UNITY_EXE%" ^
 -batchmode -nographics ^
 -projectPath "%PROJECT_PATH%" ^
 -runTests ^
 -testPlatform PlayMode ^
 -testFilter "DeliveryRun.PlayModeTests.TrafficNpcRoadRulePlayModeTest" ^
 -testResults "%RESULT_PATH%" ^
 -logFile "%LOG_PATH%" ^
 -playerHeartbeatTimeout 600

set "EXIT_CODE=%ERRORLEVEL%"
echo [RunTests_PlayMode] Unity ExitCode=%EXIT_CODE%
if exist "%RESULT_PATH%" (
  echo [RunTests_PlayMode] TestResults: %RESULT_PATH%
) else (
  echo [RunTests_PlayMode] TestResults not found.
)
echo [RunTests_PlayMode] End: %DATE% %TIME%
exit /b %EXIT_CODE%
