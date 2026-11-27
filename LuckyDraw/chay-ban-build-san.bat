@echo off
REM ======================================================================
REM  LuckyDraw - chay ban build san (khong can build, khong can NuGet)
REM  bin\ khong co trong git: build o may co mang (dotnet build) roi chep ca thu muc LuckyDraw sang
REM  Can: ASP.NET Core Runtime 8 (hoac .NET 8 SDK) + mang toi database
REM
REM  Dat chuoi ket noi 1 lan (mo cmd moi sau khi chay lenh nay):
REM    setx ConnectionStrings__LuckyDraw "Host=...;Port=5432;Database=postgres;Username=luckydraw_app.<project-ref>;Password=...;SSL Mode=Require;Search Path=luckydraw"
REM  Lan dau (database chua co tai khoan nao) them mat khau admin:
REM    setx Seed__AdminPassword "mat-khau-admin-toi-thieu-8-ky-tu"
REM
REM  HTTPS (tuy chon): chay tao-chung-chi-https.bat 1 lan -> file nay tu bat HTTPS o cong PORT+1 (5281),
REM  http://...:PORT tu chuyen sang https. Chua co chung chi thi chay HTTP nhu cu.
REM ======================================================================
setlocal
cd /d "%~dp0LuckyDraw.Web"

if "%ConnectionStrings__LuckyDraw%"=="" (
    echo [LOI] Chua dat bien moi truong ConnectionStrings__LuckyDraw.
    echo       Xem huong dan o dau file nay hoac trong HUONG-DAN-SETUP.md muc 8.
    pause
    exit /b 1
)

if not exist "bin\Debug\net8.0\LuckyDraw.Web.dll" (
    echo [LOI] Khong thay bin\Debug\net8.0\LuckyDraw.Web.dll
    echo       bin\ khong co trong git - chay "dotnet build" trong LuckyDraw.Web, hoac chep bin tu may da build.
    pause
    exit /b 1
)

set PORT=5280
if not "%~1"=="" set PORT=%~1
set /a HTTPS_PORT=%PORT%+1

if not exist "certs\luckydraw.pfx" goto http
if "%LuckyDraw__Https__CertPassword%"=="" goto nopass

:https
echo LuckyDraw dang chay:  https://localhost:%HTTPS_PORT%   (Admin: https://localhost:%HTTPS_PORT%/admin)
echo May khac trong mang LAN mo: https://^<IP may nay^>:%HTTPS_PORT%   - Bam Ctrl+C de dung.
echo May khac can cai chung chi 1 lan: cai-chung-chi-may-khach.bat + luckydraw.cer
echo.
REM --contentRoot phai la duong dan TUYET DOI (duong dan tuong doi bi tinh theo thu muc chua file .dll)
dotnet "bin\Debug\net8.0\LuckyDraw.Web.dll" --contentRoot "%CD%" --LuckyDraw:Https:Enabled=true --LuckyDraw:Https:HttpPort=%PORT% --LuckyDraw:Https:Port=%HTTPS_PORT%
pause
exit /b

:nopass
echo [CANH BAO] Co certs\luckydraw.pfx nhung chua co mat khau chung chi trong bien moi truong.
echo            Chay lai tao-chung-chi-https.bat roi mo cua so MOI. Tam chay HTTP.
echo.

:http
echo LuckyDraw dang chay:  http://localhost:%PORT%   (Admin: http://localhost:%PORT%/admin)
echo May khac trong mang LAN mo: http://^<IP may nay^>:%PORT%   - Bam Ctrl+C de dung.
echo.
REM --contentRoot phai la duong dan TUYET DOI (duong dan tuong doi bi tinh theo thu muc chua file .dll)
dotnet "bin\Debug\net8.0\LuckyDraw.Web.dll" --contentRoot "%CD%" --urls "http://0.0.0.0:%PORT%"
pause
