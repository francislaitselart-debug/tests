@echo off
rem Compilation du generateur avec le csc du .NET Framework 4.x (aucun SDK requis)
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
"%CSC%" /nologo /optimize /target:exe /out:GenerateurBarre.exe /r:System.Drawing.dll /r:System.Xml.dll /r:System.Xml.Linq.dll /r:System.Core.dll GenerateurBarre.cs
if errorlevel 1 exit /b 1
copy /y ..\barre-filtres.xsd . >nul
rem Exemple :
rem GenerateurBarre.exe ..\exemple-barre.xml /racine:"C:\MonProjetPanorama\Images" /nettoyer
