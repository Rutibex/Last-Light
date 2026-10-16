@echo off
cd /d "%~dp0"
Lastlight.exe %*
if errorlevel 1 (
  echo.
  echo Lastlight exited with an error. See the message above or
  echo %LOCALAPPDATA%\Lastlight\Saves\startup-error.txt
  pause
)
