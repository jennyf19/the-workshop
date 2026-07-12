@echo off
REM Start.bat -- build the workshop and launch the system-tray app. No console
REM is left running: the app lives as a hammer-and-wrench icon in the Windows
REM notification area (click the ^ arrow near the clock if you don't see it).
REM The tray starts the web server hidden, opens the dashboard in your browser,
REM and its menu lets you Open or Exit.
REM
REM Both the tray and the web run from a staged copy under .run\ (not from
REM src\...\bin), and the tray is pointed at the staged web via WORKSHOP_WEB_EXE.
REM That keeps the source build output writable, so `dotnet build` / `dotnet
REM test` never fail with "file in use by WorkshopRoom" while the app is up.
REM
REM Operator state (closed/archived desks, dismissed alerts, ...) lives in
REM %LocalAppData%\the-workshop, so it persists across staged runs.
REM
REM Run from anywhere -- this script cd's to its own directory.
REM
REM Dev tip: to run the web directly with a live console instead, use
REM `dotnet run --project src\WorkshopRoom` (see README).

setlocal

set "RUN_WEB=.run\web"
set "RUN_TRAY=.run\tray"
set "TRAY_EXE=%RUN_TRAY%\WorkshopRoom.Tray.exe"

pushd "%~dp0"

echo.
echo [1/3] Building the-workshop.slnx ...
echo.
dotnet build the-workshop.slnx --nologo
if errorlevel 1 (
    echo.
    echo Build failed. See errors above.
    popd
    endlocal
    exit /b 1
)

echo.
echo [2/3] Staging tray + web to .run\ ^(keeps src\...\bin free for rebuilds^)...
echo.
dotnet publish "src\WorkshopRoom" -c Debug -o "%RUN_WEB%" --no-build --nologo
if errorlevel 1 goto stage_failed
dotnet publish "src\WorkshopRoom.Tray" -c Debug -o "%RUN_TRAY%" --no-build --nologo
if errorlevel 1 goto stage_failed

if not exist "%TRAY_EXE%" (
    echo.
    echo Could not find %TRAY_EXE% after staging.
    popd
    endlocal
    exit /b 1
)

REM Point the tray at the staged web exe so the running web never locks
REM src\WorkshopRoom\bin. Absolute path -- the tray's working dir differs.
set "WORKSHOP_WEB_EXE=%~dp0%RUN_WEB%\WorkshopRoom.exe"

echo.
echo [3/3] Launching the workshop tray ^(look for the hammer-and-wrench icon near the clock^)...
start "" "%TRAY_EXE%"

popd
endlocal
exit /b 0

:stage_failed
echo.
echo Staging to .run\ failed -- a workshop instance may already be running and
echo is locking the staged copy. Quit it from the tray menu (or close it), then
echo re-run Start.bat.
popd
endlocal
exit /b 1
