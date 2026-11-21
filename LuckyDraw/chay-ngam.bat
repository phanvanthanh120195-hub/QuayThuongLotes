@echo off
REM ======================================================================
REM  LuckyDraw - chay NGAM (khong hien cua so). Dung app: chay dung-app.bat
REM  Giong chay-ban-build-san.bat: can bin\ da build + bien ConnectionStrings__LuckyDraw
REM  Log ghi vao LuckyDraw.Web\logs\app.log (xem khi co loi)
REM ======================================================================
setlocal
cd /d "%~dp0LuckyDraw.Web"

if "%ConnectionStrings__LuckyDraw%"=="" (
    echo [LOI] Chua dat bien moi truong ConnectionStrings__LuckyDraw. Xem chay-ban-build-san.bat.
    pause
    exit /b 1
)
if not exist "bin\Debug\net8.0\LuckyDraw.Web.dll" (
    echo [LOI] Khong thay bin\Debug\net8.0\LuckyDraw.Web.dll - chay "dotnet build" trong LuckyDraw.Web truoc.
    pause
    exit /b 1
)

set PORT=5280
if not "%~1"=="" set PORT=%~1
if not exist logs mkdir logs

powershell -NoProfile -Command "Start-Process -FilePath 'dotnet' -ArgumentList 'bin\Debug\net8.0\LuckyDraw.Web.dll','--contentRoot','\"%CD%\"','--urls','http://0.0.0.0:%PORT%' -WorkingDirectory '%CD%' -WindowStyle Hidden -RedirectStandardOutput 'logs\app.log' -RedirectStandardError 'logs\app-error.log'"

REM cho app khoi dong roi mo trinh duyet
timeout /t 4 /nobreak >nul
start "" "http://localhost:%PORT%/"
echo LuckyDraw dang chay ngam: http://localhost:%PORT%   (Admin: /admin)
echo Dung app: chay dung-app.bat
timeout /t 5 >nul
