@echo off
REM ===================================================================
REM  Boxing Day :: batch-convert every pallet .svg in THIS folder -> .png
REM  Uses Inkscape 1.x CLI. Each SVG keeps its own document size, so the
REM  PNG comes out at the texture's native pixel size (no -w/-h needed).
REM  Double-click to run, or run from a terminal in this folder.
REM ===================================================================
setlocal enabledelayedexpansion

REM --- locate Inkscape (PATH first, then the usual install spots) ---
set "INK="
where inkscape.com >nul 2>nul && set "INK=inkscape.com"
if not defined INK if exist "C:\Program Files\Inkscape\bin\inkscape.com" set "INK=C:\Program Files\Inkscape\bin\inkscape.com"
if not defined INK if exist "C:\Program Files\Inkscape\bin\inkscape.exe" set "INK=C:\Program Files\Inkscape\bin\inkscape.exe"
if not defined INK if exist "C:\Program Files (x86)\Inkscape\bin\inkscape.com" set "INK=C:\Program Files (x86)\Inkscape\bin\inkscape.com"
if not defined INK (
  echo [ERROR] Could not find Inkscape. Install it or add inkscape.com to PATH.
  pause
  exit /b 1
)
echo Using Inkscape: !INK!

set "DIR=%~dp0"
set /a COUNT=0
for %%F in ("%DIR%*.svg") do (
  echo Converting %%~nxF
  "!INK!" "%%~fF" --export-type=png --export-filename="%%~dpnF.png"
  set /a COUNT+=1
)

echo.
echo Done. Converted !COUNT! file(s) to PNG in:
echo   %DIR%
pause
