@echo off
rem Headless EditMode test runner. From repo root:  Tools\headless\run-tests.cmd
rem (See run-tests.ps1 / README.md for details.)
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-tests.ps1" %*
exit /b %ERRORLEVEL%
