@echo off
rem Compilation avec le compilateur fourni par le .NET Framework 4.x (aucun SDK requis)
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
"%CSC%" /nologo /optimize /target:exe /out:LargeursLibelles.exe /r:System.Drawing.dll /r:System.Windows.Forms.dll LargeursLibelles.cs
if errorlevel 1 exit /b 1
rem Exemple :
rem LargeursLibelles.exe exemple-libelles.csv /police:"Segoe UI" /taille:9 /unite:pt /csv:largeurs.csv /vbs:Largeurs.vbs
