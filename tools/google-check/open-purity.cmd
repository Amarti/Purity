@echo off
rem Opens Purity as "@COMPUTER@", one of two make-believe computers on this PC. Its settings, its
rem Google sign-in and its copy of the made-up history stay in the "@COMPUTER@" folder beside this
rem file, apart from any real Purity. Delete that folder to start this computer afresh.
setlocal
set "kit=%~dp0"
set "computer=%kit%@COMPUTER@"
if exist "%computer%\history.json" goto open
mkdir "%computer%" 2> nul
copy /y "%kit%history.json" "%computer%\history.json" > nul
:open
set "PURITY_APP_DATA=%computer%\app data"
start "" "%kit%Purity.exe" --data "%computer%\history.json"
