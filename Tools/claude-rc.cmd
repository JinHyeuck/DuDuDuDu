@echo off
rem Start Claude Remote Control for this project.  Double-click, or:  Tools\claude-rc.cmd
rem   no args   -> claude rc --spawn=worktree
rem   any args  -> passed to claude rc as-is (e.g. --spawn=same-dir)
rem
rem Always runs in the MAIN checkout's DuDuDuDU_Project, even when this file is
rem launched from inside a git worktree: git-common-dir points at the main .git.
rem
rem worktree spawn by default: the main checkout sits on main with uncommitted
rem art changes, and same-dir would let remote sessions commit on top of them.
rem
rem Closing this window (or Ctrl+C) ends Remote Control.
rem This file stays ASCII so it prints correctly under any console codepage.
setlocal
pushd "%~dp0.."

for /f "delims=" %%G in ('git rev-parse --path-format^=absolute --git-common-dir') do set "GITDIR=%%G"
if not defined GITDIR (
    echo [claude-rc] not inside a git repository: %CD%
    popd & pause & exit /b 1
)
popd

pushd "%GITDIR%\..\DuDuDuDU_Project" || (
    echo [claude-rc] project folder not found under %GITDIR%\..
    pause & exit /b 1
)

echo [claude-rc] %CD%
if "%~1"=="" (
    claude rc --spawn=worktree
) else (
    claude rc %*
)
set RC=%ERRORLEVEL%

popd
if not "%RC%"=="0" pause
exit /b %RC%
