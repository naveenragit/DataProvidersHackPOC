@echo off
REM Runs the Prism ASP.NET Core API on http://localhost:8000
cd /d "%~dp0backend\FinancialServices.Api"
dotnet restore
dotnet run --urls http://localhost:8000
