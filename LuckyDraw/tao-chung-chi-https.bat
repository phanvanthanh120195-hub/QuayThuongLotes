@echo off
REM ======================================================================
REM  LuckyDraw - tao chung chi HTTPS cho may chu trong mang LAN (chay 1 lan)
REM  Chay tren MAY CHAY APP. Khong can quyen Administrator.
REM  Sau khi tao: chay-ban-build-san.bat / chay-ngam.bat tu bat HTTPS (cong 5281).
REM  Doi IP may chu thi chay lai file nay va cai lai chung chi tren may khach.
REM ======================================================================
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\tao-chung-chi-https.ps1"
echo.
pause
