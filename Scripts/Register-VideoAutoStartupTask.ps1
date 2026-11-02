<#
.SYNOPSIS
    Đăng ký Windows Task Scheduler tự động khởi động VideoAutoWpf khi đăng nhập Windows (At log on).

.DESCRIPTION
    Script này cấu hình một Scheduled Task chuyên dụng cho mini PC chạy tự động:
    - Trigger: At log on (Khi người dùng đăng nhập vào Windows Desktop)
    - Delay khởi động: 30 giây (đảm bảo card mạng, audio, GPU và Google Cloud connectivity đã sẵn sàng)
    - Session: Interactive User Session (Cần thiết cho ứng dụng WPF GUI và FFmpeg phần cứng)
    - Tự động thử lại: Khởi động lại tối đa 3 lần nếu xảy ra lỗi
    - Không kill tiến trình: ExecutionTimeLimit = 0 (Chạy liên tục cho đến khi tự động shutdown)
    - Đơn instance: Không cho phép chạy 2 phiên đồng thời (kết hợp với Mutex trong app)

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Register-VideoAutoStartupTask.ps1
#>

[CmdletBinding()]
param(
    [string]$TaskName = "VideoAutoWpf_AutoRun",
    [int]$DelaySeconds = 30,
    [string]$ExePath = ""
)

$ErrorActionPreference = "Stop"

Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "  THIẾT LẬP TASK SCHEDULER TỰ ĐỘNG CHẠY CHO VIDEOAUTOWPF (MINI PC)" -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan

# 1. Xác định đường dẫn file VideoAutoWpf.exe
if ([string]::IsNullOrWhiteSpace($ExePath)) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $rootDir = Split-Path -Parent $scriptDir
    $candidates = @(
        (Join-Path $rootDir "bin\Release\net10.0-windows\VideoAutoWpf.exe"),
        (Join-Path $rootDir "bin\Debug\net10.0-windows\VideoAutoWpf.exe"),
        (Join-Path $rootDir "VideoAutoWpf.exe")
    )

    foreach ($cand in $candidates) {
        if (Test-Path $cand) {
            $ExePath = (Resolve-Path $cand).Path
            break
        }
    }
}

if (-not (Test-Path $ExePath)) {
    Write-Error "Không tìm thấy file VideoAutoWpf.exe. Vui lòng build project trước (dotnet build -c Release) hoặc chỉ định tham số -ExePath."
    exit 1
}

$WorkingDirectory = Split-Path -Parent $ExePath
Write-Host "[+] Tìm thấy Executable: $ExePath" -ForegroundColor Green
Write-Host "[+] Working Directory:  $WorkingDirectory" -ForegroundColor Green

# 2. Xóa task cũ nếu đã tồn tại
$existing = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "[!] Đang gỡ bỏ Scheduled Task cũ '$TaskName'..." -ForegroundColor Yellow
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
}

# 3. Tạo Action (Chạy EXE với Working Directory chuẩn)
$action = New-ScheduledTaskAction -Execute $ExePath -WorkingDirectory $WorkingDirectory

# 4. Tạo Trigger: At log on của tài khoản hiện tại + Delay
$currentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $currentUser
if ($DelaySeconds -gt 0) {
    $trigger.Delay = "PT${DelaySeconds}S"
}

# 5. Cấu hình Settings an toàn cao:
# - DisallowStartIfOnBatteries = False (chạy mini PC cắm nguồn)
# - StopIfGoingOnBatteries = False
# - ExecutionTimeLimit = PT0S (Không giới hạn thời gian chạy, không kill giữa chừng)
# - RestartCount = 3, RestartInterval = 1 phút (Tự động bật lại nếu crash)
# - MultipleInstances = IgnoreNew (Chỉ 1 phiên duy nhất)
$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -ExecutionTimeLimit ([TimeSpan]::Zero) `
    -RestartCount 3 `
    -RestartInterval (New-TimeSpan -Minutes 1) `
    -MultipleInstancesPolicy IgnoreNew `
    -StartWhenAvailable

# 6. Đăng ký Task vào Windows Task Scheduler
$principal = New-ScheduledTaskPrincipal -UserId $currentUser -LogonType Interactive -RunLevel Highest

Register-ScheduledTask `
    -TaskName $TaskName `
    -Action $action `
    -Trigger $trigger `
    -Settings $settings `
    -Principal $principal `
    -Description "Tự động vận hành VideoAutoWpf khi đăng nhập: tạo video AI, gửi Telegram duyệt, tự động đăng TikTok/YouTube và tắt máy an toàn."

Write-Host ""
Write-Host "==================================================================" -ForegroundColor Green
Write-Host "  ĐÃ ĐĂNG KÝ TASK SCHEDULER THÀNH CÔNG!" -ForegroundColor Green
Write-Host "  Tên Task: $TaskName" -ForegroundColor White
Write-Host "  Tài khoản: $currentUser" -ForegroundColor White
Write-Host "  Delay: $DelaySeconds giây sau khi đăng nhập" -ForegroundColor White
Write-Host "==================================================================" -ForegroundColor Green
Write-Host ""
Write-Host "LƯU Ý QUAN TRỌNG VẬN HÀNH MINI PC:" -ForegroundColor Yellow
Write-Host "1. Hãy bật tính năng 'Restore on AC Power Loss' trong BIOS/UEFI của mini PC để máy tự bật lại khi có điện." -ForegroundColor White
Write-Host "2. Ứng dụng là phần mềm giao diện WPF, do đó BẮT BUỘC chạy trong phiên Desktop (Interactive)." -ForegroundColor White
Write-Host "3. Khi không còn công việc, ứng dụng sẽ tự động kích hoạt Shutdown an toàn mà không cần chạm vào máy." -ForegroundColor White
