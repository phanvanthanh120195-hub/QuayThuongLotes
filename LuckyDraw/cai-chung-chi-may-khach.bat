@echo off
REM ======================================================================
REM  LuckyDraw - cai chung chi HTTPS tren MAY KHACH (may chieu, may admin khac...)
REM  Chep 2 file sang may khach: luckydraw.cer + cai-chung-chi-may-khach.bat (cung thu muc)
REM  roi nhap dup file nay. Windows hoi xac nhan -> bam YES. Khong can quyen Administrator.
REM  Sau do mo https://<IP may chu>:5281 tren Chrome / Edge se khong con canh bao "khong an toan".
REM  (Firefox dung kho chung chi rieng: Settings > Privacy & Security > Certificates > Import.)
REM ======================================================================
if not exist "%~dp0luckydraw.cer" (
    echo [LOI] Khong thay luckydraw.cer canh file nay.
    echo       Chep luckydraw.cer tu thu muc LuckyDraw tren may chu sang cung thu muc.
    pause
    exit /b 1
)
certutil -user -addstore Root "%~dp0luckydraw.cer"
if errorlevel 1 (
    echo [LOI] Chua cai duoc chung chi. Thu chay lai va bam YES khi Windows hoi.
) else (
    echo.
    echo Da cai chung chi. Dong het cua so trinh duyet roi mo lai trang LuckyDraw.
)
pause
