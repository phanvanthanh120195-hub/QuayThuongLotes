@echo off
REM Dung LuckyDraw dang chay ngam (chay-ngam.bat) hoac LuckyDraw.Web.exe
powershell -NoProfile -Command "$p = Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'LuckyDraw.Web.exe' -or ($_.Name -eq 'dotnet.exe' -and $_.CommandLine -like '*LuckyDraw.Web.dll*') }; if ($p) { $p | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }; Write-Host 'Da dung LuckyDraw.' } else { Write-Host 'LuckyDraw khong chay.' }"
timeout /t 3 >nul
