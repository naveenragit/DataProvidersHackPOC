@echo off
REM Runs the Prism ASP.NET Core API on http://localhost:8000
REM Loads the repo-root .env (KEY=VALUE, # comments) into the process environment so
REM Azure__* / Prism__* bind to the app's options. .NET does not read .env natively.
REM Auth: DefaultAzureCredential (browser) — run `az login` once before starting.
setlocal enabledelayedexpansion
if exist "%~dp0.env" (
  for /f "usebackq eol=# tokens=1,* delims==" %%A in ("%~dp0.env") do (
    if not "%%~A"=="" set "%%A=%%B"
  )
)
cd /d "%~dp0backend\FinancialServices.Api"
dotnet restore
dotnet run --urls http://localhost:8000
