@echo off
rem All static verification in one place. From repo root:  Tools\verify-all.cmd
rem   --quick   skip the headless test run (encoding / namespaces / missing scripts only)
rem
rem Why one entry point: four separate checks that nobody remembers to run are
rem the same as no checks. Exit code is nonzero if any of them fails.
rem
rem The Korean output comes from the Python scripts themselves; this file stays
rem ASCII so it prints correctly under any console codepage.
setlocal
pushd "%~dp0.."

set FAIL=0

echo.
python Tools\verify_encoding.py
if errorlevel 1 set /a FAIL+=1

echo.
python Tools\verify_namespaces.py
if errorlevel 1 set /a FAIL+=1

echo.
python Tools\verify_missing_scripts.py --baseline 0
if errorlevel 1 set /a FAIL+=1

echo.
python Tools\verify_singleton_count.py
if errorlevel 1 set /a FAIL+=1

if /i "%~1"=="--quick" goto :done

echo.
call Tools\headless\run-tests.cmd
if errorlevel 1 set /a FAIL+=1

:done
echo.
if "%FAIL%"=="0" (
    echo [verify-all] PASS
) else (
    echo [verify-all] FAIL - %FAIL% check^(s^) failed
)
popd
exit /b %FAIL%
