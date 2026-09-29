sc create CameraTogglerService binPath= %CD%\MakeWindowsHelloGreatAgain.exe obj= LocalSystem start= system
REM documentation says EventLog source registration is async and should not be relied on immediately after registering. 
REM So, just restart the service once after 5 seconds 
timeout 5
if "%ERRORLEVEL%" == "0" sc stop CameraTogglerService
if "%ERRORLEVEL%" == "0" sc start CameraTogglerService
