@echo off
REM Barometer_UWP dependency bootstrap for VS2017 (classic UAP + packages.config).
REM Usage: double-click or run from repo root. Requires internet once.
setlocal
cd /d %~dp0
where nuget >nul 2>&1
if errorlevel 1 (
  echo [1/3] Downloading nuget.exe ...
  powershell -NoProfile -Command "Invoke-WebRequest https://dist.nuget.org/win-x86-commandline/v5.11.6/nuget.exe -OutFile nuget.exe" || goto :err
) else ( echo [1/3] nuget.exe found on PATH )
echo [2/3] v1.4.0.36: project has NO compile-time NuGet dependencies (vendored JSON + SDK zip/xlsx).
echo       Restore is OPTIONAL and only fetches Microsoft.Net.Native.Compiler 1.7.3 for Release/.NET Native.
nuget restore Barometer_UWP.sln || echo       restore skipped/failed - Debug build works without it.
echo [3/3] Done. Open Barometer_UWP.sln in Visual Studio 2017, create the test signing
echo       certificate on the Signing page (first build), then Build Release^|ARM.
exit /b 0
:err
echo RESTORE FAILED — check network and that this is the repo root folder.
exit /b 1
