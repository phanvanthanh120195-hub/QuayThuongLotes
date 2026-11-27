# =====================================================================
# LuckyDraw – tạo chứng chỉ HTTPS tự ký cho máy chủ trong mạng LAN.
# Chạy bằng tao-chung-chi-https.bat (nhấp đúp) trên MÁY CHẠY APP. Không cần quyền Administrator.
#   1. Lấy tên máy + các địa chỉ IPv4 trong LAN → đưa vào chứng chỉ (trình duyệt kiểm tra đúng địa chỉ).
#   2. Xuất LuckyDraw.Web\certs\luckydraw.pfx (khoá bí mật – KHÔNG chia sẻ, không đưa lên git)
#      và luckydraw.cer (chứng chỉ công khai) cạnh file cai-chung-chi-may-khach.bat để cài cho máy khác.
#   3. Lưu mật khẩu .pfx vào biến môi trường LuckyDraw__Https__CertPassword (giống chuỗi kết nối DB).
#   4. Cài tin cậy chứng chỉ trên chính máy này (Windows hỏi xác nhận → bấm Yes).
# Đổi IP máy chủ (vd. sang mạng khác) thì chạy lại script này rồi cài lại chứng chỉ trên các máy khách.
# =====================================================================
param([int]$Days = 730)
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot                 # thư mục LuckyDraw
$certDir = Join-Path $root 'LuckyDraw.Web\certs'
$pfxPath = Join-Path $certDir 'luckydraw.pfx'
$cerPath = Join-Path $root 'luckydraw.cer'
New-Item -ItemType Directory -Force $certDir | Out-Null

# --- Tên máy + IP trong LAN (bỏ loopback, APIPA 169.254.x.x) ---
$pcName = $env:COMPUTERNAME
$ips = @(Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' } |
    Select-Object -ExpandProperty IPAddress -Unique)
Write-Host ''
Write-Host "May chu : $pcName"
Write-Host ("Dia chi IP: " + ($(if ($ips.Count) { $ips -join ', ' } else { '(khong tim thay)' })))

$san = @("DNS=$pcName", "DNS=localhost", "IPAddress=127.0.0.1") + ($ips | ForEach-Object { "IPAddress=$_" })
# Ngoặc quanh phần tử đầu là bắt buộc: không có thì PowerShell nối cả 3 phần tử thành 1 chuỗi
$ext = @(
    ('2.5.29.17={text}' + ($san -join '&')),      # Subject Alternative Name: tên máy + các IP
    '2.5.29.37={text}1.3.6.1.5.5.7.3.1',          # chỉ dùng cho máy chủ web (Server Authentication)
    '2.5.29.19={critical}{text}ca=false'           # không phải CA (giống chứng chỉ dev của .NET)
)

# --- Xoá chứng chỉ LuckyDraw cũ (tạo lần trước) để không bị trùng ---
Get-ChildItem Cert:\CurrentUser\My, Cert:\CurrentUser\Root -ErrorAction SilentlyContinue |
    Where-Object { $_.FriendlyName -eq 'LuckyDraw HTTPS (LAN)' } |
    ForEach-Object { Remove-Item -LiteralPath $_.PSPath -ErrorAction SilentlyContinue }

$cert = New-SelfSignedCertificate -Subject "CN=LuckyDraw ($pcName)" -FriendlyName 'LuckyDraw HTTPS (LAN)' `
    -TextExtension $ext -KeyAlgorithm RSA -KeyLength 2048 -HashAlgorithm SHA256 `
    -KeyUsage DigitalSignature, KeyEncipherment -KeyExportPolicy Exportable `
    -NotAfter (Get-Date).AddDays($Days) -CertStoreLocation Cert:\CurrentUser\My

# --- Mật khẩu ngẫu nhiên cho file .pfx ---
$bytes = New-Object byte[] 24
[Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
$password = [Convert]::ToBase64String($bytes).Replace('+', 'A').Replace('/', 'B').Replace('=', '')
$secure = ConvertTo-SecureString $password -AsPlainText -Force

Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $secure | Out-Null
Export-Certificate -Cert $cert -FilePath $cerPath -Type CERT | Out-Null
Remove-Item -LiteralPath $cert.PSPath                    # đã xuất ra file, không cần giữ trong kho "My"

# --- Lưu mật khẩu (vĩnh viễn cho tài khoản Windows này) ---
[Environment]::SetEnvironmentVariable('LuckyDraw__Https__CertPassword', $password, 'User')

# --- Tin cậy chứng chỉ trên chính máy chủ (Windows sẽ hỏi xác nhận) ---
Write-Host ''
Write-Host 'Windows se hoi "Ban co muon cai chung chi nay khong" -> bam YES.'
Import-Certificate -FilePath $cerPath -CertStoreLocation Cert:\CurrentUser\Root | Out-Null

Write-Host ''
Write-Host '=== XONG ==='
Write-Host "Chung chi (khoa bi mat): $pfxPath"
Write-Host "Chung chi cho may khach : $cerPath"
Write-Host ("Het han: " + $cert.NotAfter.ToString('dd/MM/yyyy'))
Write-Host ''
Write-Host 'Tiep theo:'
Write-Host '  1. Mo cua so moi, chay chay-ban-build-san.bat hoac chay-ngam.bat -> app tu bat HTTPS.'
foreach ($ip in $ips) { Write-Host "     May khac mo: https://${ip}:5281" }
Write-Host '  2. Moi may khac mo app: chep luckydraw.cer + cai-chung-chi-may-khach.bat sang, nhap dup file .bat.'
