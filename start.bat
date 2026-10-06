@echo off
cd /d "%~dp0"
dotnet restore UnposVardiyaTakip.sln
dotnet run --project src\UnposVardiyaTakip.Win\UnposVardiyaTakip.Win.csproj -c Release
pause
