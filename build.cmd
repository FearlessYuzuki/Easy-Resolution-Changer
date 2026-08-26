@echo off
chcp 65001 >nul
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo 未找到 .NET Framework 4.x 的 csc.exe
  pause
  exit /b 1
)
set REF=%ProgramFiles(x86)%\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8
if not exist "%REF%\PresentationFramework.dll" set REF=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\WPF
"%CSC%" /nologo /codepage:65001 /target:winexe /out:ResolutionChanger.exe /win32manifest:app.manifest ^
  /r:"%REF%\System.dll" /r:"%REF%\System.Core.dll" /r:"%REF%\WindowsBase.dll" /r:"%REF%\PresentationCore.dll" ^
  /r:"%REF%\PresentationFramework.dll" /r:"%REF%\System.Xaml.dll" ^
  Program.cs DisplayInfo.cs MainWindow.cs
if errorlevel 1 (
  echo 编译失败
  pause
) else (
  echo 编译成功: ResolutionChanger.exe
)
