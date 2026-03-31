@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ProgramFiles%\Vassar College\Asset Tool\uninstall.ps1" -Quiet
exit /b %errorlevel%
