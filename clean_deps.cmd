@echo off
REM v1.4.0.36: one-time machine cleanup for the WMC1006/WMC9999 'System.Runtime' shadowing bug.
REM Run this ONCE from an elevated OR normal console BEFORE building Barometer_UWP.sln.
setlocal
echo [1/2] Removing NuGet facade copies (System.* DLLs) from the global packages folder...
del /q /s "%USERPROFILE%\.nuget\packages\system.*\*\lib\netcore50\System.*.dll" 2>nul
for /d %%D in ("%USERPROFILE%\.nuget\packages\system.*") do rmdir /s /q "%%D" 2>nul
if exist "%USERPROFILE%\.nuget\packages\microsoft.netcore.universalwindowsplatform" rmdir /s /q "%USERPROFILE%\.nuget\packages\microsoft.netcore.universalwindowsplatform"
echo     Done. (Only System.* / UWP-metapackage folders are touched; other packages are kept.)
echo [2/2] Removing stale local packages folder next to the solution (if any)...
if exist "%~dp0packages" rmdir /s /q "%~dp0packages"
echo.
echo Now open Barometer_UWP.sln and Rebuild Solution (Debug^|ARM). No restore is required:
echo the project compiles against Windows SDK reference assemblies only.
endlocal
