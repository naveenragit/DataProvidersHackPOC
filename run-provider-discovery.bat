@echo off
REM Runs the Phase-0 live-provider MCP discovery CLI (implementationPlan/13, Round 1).
REM Loads the repo-root .env (KEY=VALUE, # comments) into the process environment so
REM Prism__Providers__* bind to the CLI's options. .NET does not read .env natively.
REM
REM Usage:  run-provider-discovery.bat --provider morningstar
REM         run-provider-discovery.bat --provider moodys --call ^<toolName^>
REM
REM Prereqs: set Prism__Providers__<Name>__Enabled=true + ClientId + ClientSecret in .env first.
REM A browser opens for a ONE-TIME interactive sign-in; the refresh token is cached under
REM .prism\ (git-ignored). The findings note is written under .prism\discovery\ (git-ignored).
setlocal enabledelayedexpansion
if exist "%~dp0.env" (
  for /f "usebackq eol=# tokens=1,* delims==" %%A in ("%~dp0.env") do (
    if not "%%~A"=="" set "%%A=%%B"
  )
)
cd /d "%~dp0"
dotnet run --project tools\ProviderDiscovery -- %*
