@echo off
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo [x] .NET Framework csc.exe not found
  exit /b 1
)
cd /d "%~dp0"
if not exist bin mkdir bin

set REFS=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
if not exist "%REFS%\System.dll" set REFS=%WINDIR%\Microsoft.NET\Framework\v4.0.30319

"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu ^
  /out:bin\QuickMix.exe ^
  /r:"%REFS%\System.dll" ^
  /r:"%REFS%\System.Core.dll" ^
  /r:"%REFS%\System.Drawing.dll" ^
  /r:"%REFS%\System.Windows.Forms.dll" ^
  src\QuickMix.cs

if errorlevel 1 (
  echo [x] build failed
  exit /b 1
)
echo [ok] bin\QuickMix.exe
endlocal
