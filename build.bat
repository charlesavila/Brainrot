@echo off
setlocal
dotnet build "%~dp0Brainrot.csproj" -c Release
if errorlevel 1 exit /b 1

set "OUT=%~dp0bin\Win64_Shipping_Client"
if not exist "%OUT%" mkdir "%OUT%"
copy /Y "%~dp0bin\Release\Brainrot.dll" "%OUT%\Brainrot.dll" >nul
if errorlevel 1 exit /b 1
copy /Y "%~dp0bin\Release\Brainrot.pdb" "%OUT%\Brainrot.pdb" >nul
if errorlevel 1 exit /b 1

set "OUT=%~dp0bin\Win64_Shipping_wEditor"
if not exist "%OUT%" mkdir "%OUT%"
copy /Y "%~dp0bin\Release\Brainrot.dll" "%OUT%\Brainrot.dll" >nul
if errorlevel 1 exit /b 1
copy /Y "%~dp0bin\Release\Brainrot.pdb" "%OUT%\Brainrot.pdb" >nul
if errorlevel 1 exit /b 1
echo Built Brainrot.dll
