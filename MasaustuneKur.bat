@echo off
setlocal
set "HEDEF=%USERPROFILE%\Desktop\Akaryakit Proje"
echo Masaustune kopyalaniyor:
echo   %HEDEF%
mkdir "%HEDEF%" 2>nul
mkdir "%HEDEF%\samples" 2>nul
mkdir "%HEDEF%\Kaynak" 2>nul
xcopy /E /I /Y "%~dp0*" "%HEDEF%\" >nul
echo.
echo Tamam. Klasor: %HEDEF%
echo Programi acmak icin UnposVardiyaTakip.exe
pause
explorer "%HEDEF%"
endlocal
