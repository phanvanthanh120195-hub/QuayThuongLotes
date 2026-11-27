@echo off
REM ======================================================================
REM  LuckyDraw - chay NGAM (khong hien cua so). Dung app: chay dung-app.bat
REM  Giong chay-ban-build-san.bat: can bin\ da build + bien ConnectionStrings__LuckyDraw
REM  Log ghi vao LuckyDraw.Web\logs\app.log (xem khi co loi)
REM  Co chung chi (tao-chung-chi-https.bat) thi tu bat HTTPS o cong PORT+1 (5281).
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
set /a HTTPS_PORT=%PORT%+1
if not exist logs mkdir logs

if not exist "certs\luckydraw.pfx" goto http
if "%LuckyDraw__Https__CertPassword%"=="" goto nopass

:https
set OPEN_URL=https://localhost:%HTTPS_PORT%/
powershell -NoProfile -Command "Start-Process -FilePath 'dotnet' -ArgumentList 'bin\Debug\net8.0\LuckyDraw.Web.dll','--contentRoot','\"%CD%\"','--LuckyDraw:Https:Enabled=true','--LuckyDraw:Https:HttpPort=%PORT%','--LuckyDraw:Https:Port=%HTTPS_PORT%' -WorkingDirectory '%CD%' -WindowStyle Hidden -RedirectStandardOutput 'logs\app.log' -RedirectStandardError 'logs\app-error.log'"
goto started

:nopass
echo [CANH BAO] Co certs\luckydraw.pfx nhung chua co mat khau chung chi trong bien moi truong.
echo            Chay lai tao-chung-chi-https.bat roi mo cua so MOI. Tam chay HTTP.

:http
set OPEN_URL=http://localhost:%PORT%/
powershell -NoProfile -Command "Start-Process -FilePath 'dotnet' -ArgumentList 'bin\Debug\net8.0\LuckyDraw.Web.dll','--contentRoot','\"%CD%\"','--urls','http://0.0.0.0:%PORT%' -WorkingDirectory '%CD%' -WindowStyle Hidden -RedirectStandardOutput 'logs\app.log' -RedirectStandardError 'logs\app-error.log'"

:started
REM cho app khoi dong roi mo trinh duyet
timeout /t 4 /nobreak >nul
start "" "%OPEN_URL%"
echo LuckyDraw dang chay ngam: %OPEN_URL%   (Admin: %OPEN_URL%admin)
echo Dung app: chay dung-app.bat
timeout /t 5 >nul
