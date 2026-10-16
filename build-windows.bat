@echo off
cd /d "%~dp0"
dotnet publish src\Lastlight.Desktop\Lastlight.Desktop.csproj -c Release -r win-x64 --self-contained true -o artifacts\Windows-x64
if errorlevel 1 pause
