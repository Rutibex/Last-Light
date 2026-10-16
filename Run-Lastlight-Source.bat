@echo off
cd /d "%~dp0"
dotnet run --project src\Lastlight.Desktop -c Release -- %*
if errorlevel 1 (
  echo .NET 8 SDK and restored MonoGame packages are required.
  pause
)
