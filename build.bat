@echo off
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /out:AutoOCX.exe /win32manifest:app.manifest /win32icon:app.ico /r:System.Windows.Forms.dll /r:System.Drawing.dll Program.cs AssemblyInfo.cs
if %errorlevel%==0 (echo Compilado: AutoOCX.exe) else (echo Error de compilacion)
