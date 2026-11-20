@echo off
REM ======================================================================
REM  LuckyDraw - chay ban build san trong git (khong can build, khong can NuGet)
REM  Can: ASP.NET Core Runtime 8 (hoac .NET 8 SDK) + mang toi database
REM
REM  Dat chuoi ket noi 1 lan (mo cmd moi sau khi chay lenh nay):
REM    setx ConnectionStrings__LuckyDraw "Host=...;Port=5432;Database=postgres;Username=luckydraw_app.<project-ref>;Password=...;SSL Mode=Require;Search Path=luckydraw"
REM  Lan dau (database chua co tai khoan nao) them mat khau admin:
REM    setx Seed__AdminPassword "mat-khau-admin-toi-thieu-8-ky-tu"
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
    pause
    exit /b 1
)

set PORT=5280
if not "%~1"=="" set PORT=%~1

echo LuckyDraw dang chay:  http://localhost:%PORT%   (Admin: http://localhost:%PORT%/admin)
echo May khac trong mang LAN mo: http://^<IP may nay^>:%PORT%   - Bam Ctrl+C de dung.
echo.
REM --contentRoot phai la duong dan TUYET DOI (duong dan tuong doi bi tinh theo thu muc chua file .dll)
dotnet "bin\Debug\net8.0\LuckyDraw.Web.dll" --contentRoot "%CD%" --urls "http://0.0.0.0:%PORT%"
pause
