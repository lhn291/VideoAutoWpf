<#
.SYNOPSIS
    Gỡ bỏ Scheduled Task tự động chạy của VideoAutoWpf.
#>

[CmdletBinding()]
param(
    [string]$TaskName = "VideoAutoWpf_AutoRun"
)

$existing = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
if ($existing) {
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
    Write-Host "✅ Đã gỡ bỏ Scheduled Task '$TaskName' thành công." -ForegroundColor Green
} else {
    Write-Host "ℹ️ Không tìm thấy Scheduled Task '$TaskName'." -ForegroundColor Yellow
}
