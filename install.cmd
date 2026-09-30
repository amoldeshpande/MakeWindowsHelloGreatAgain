sc create CameraTogglerService binPath= %~dp0\MakeWindowsHelloGreatAgain.exe obj= LocalSystem start= auto
if "%ERRORLEVEL%" NEQ "0" exit /b
REM documentation says EventLog source registration is async and should not be relied on immediately after registering. 
REM So, just restart the service once after 5 seconds 
if "%ERRORLEVEL%" == "0" sc start CameraTogglerService
timeout 5
if "%ERRORLEVEL%" == "0" sc stop CameraTogglerService
if "%ERRORLEVEL%" == "0" sc start CameraTogglerService
