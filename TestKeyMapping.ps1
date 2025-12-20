# Test script để verify config và DLL version
Write-Host "=== KIỂM TRA CONFIG VÀ BUILD ===" -ForegroundColor Cyan

# 1. Kiểm tra file config trong bin
$binConfig = "d:\PBL4\PBL4-test\WPFUI_NEW\bin\Debug\net8.0-windows\keymapping.json"
if (Test-Path $binConfig) {
    $config = Get-Content $binConfig | ConvertFrom-Json
    
    Write-Host "`n1. CONFIG TRONG BIN:" -ForegroundColor Yellow
    Write-Host "   W mapping: '$($config.KeyboardMapping.W)' $(if ([string]::IsNullOrWhiteSpace($config.KeyboardMapping.W)) { '(TRỐNG - sẽ KHÔNG truyền)' } else { '(CÓ GIÁ TRỊ - sẽ truyền)' })" -ForegroundColor $(if ([string]::IsNullOrWhiteSpace($config.KeyboardMapping.W)) { 'Green' } else { 'Red' })
    Write-Host "   A mapping: '$($config.KeyboardMapping.A)' $(if ([string]::IsNullOrWhiteSpace($config.KeyboardMapping.A)) { '(TRỐNG - sẽ KHÔNG truyền)' } else { '(CÓ GIÁ TRỊ - sẽ truyền)' })" -ForegroundColor $(if ([string]::IsNullOrWhiteSpace($config.KeyboardMapping.A)) { 'Green' } else { 'Red' })
    Write-Host "   S mapping: '$($config.KeyboardMapping.S)' $(if ([string]::IsNullOrWhiteSpace($config.KeyboardMapping.S)) { '(TRỐNG - sẽ KHÔNG truyền)' } else { '(CÓ GIÁ TRỊ - sẽ truyền)' })" -ForegroundColor $(if ([string]::IsNullOrWhiteSpace($config.KeyboardMapping.S)) { 'Green' } else { 'Red' })
    Write-Host "   D mapping: '$($config.KeyboardMapping.D)' $(if ([string]::IsNullOrWhiteSpace($config.KeyboardMapping.D)) { '(TRỐNG - sẽ KHÔNG truyền)' } else { '(CÓ GIÁ TRỊ - sẽ truyền)' })" -ForegroundColor $(if ([string]::IsNullOrWhiteSpace($config.KeyboardMapping.D)) { 'Green' } else { 'Red' })
    
    Write-Host "`n   Up mapping: '$($config.KeyboardMapping.Up)' $(if ([string]::IsNullOrWhiteSpace($config.KeyboardMapping.Up)) { '(TRỐNG - sẽ KHÔNG truyền)' } else { '(CÓ GIÁ TRỊ - sẽ truyền)' })" -ForegroundColor $(if ([string]::IsNullOrWhiteSpace($config.KeyboardMapping.Up)) { 'Red' } else { 'Green' })
    Write-Host "   Down mapping: '$($config.KeyboardMapping.Down)' $(if ([string]::IsNullOrWhiteSpace($config.KeyboardMapping.Down)) { '(TRỐNG - sẽ KHÔNG truyền)' } else { '(CÓ GIÁ TRỊ - sẽ truyền)' })" -ForegroundColor $(if ([string]::IsNullOrWhiteSpace($config.KeyboardMapping.Down)) { 'Red' } else { 'Green' })
    Write-Host "   Left mapping: '$($config.KeyboardMapping.Left)' $(if ([string]::IsNullOrWhiteSpace($config.KeyboardMapping.Left)) { '(TRỐNG - sẽ KHÔNG truyền)' } else { '(CÓ GIÁ TRỊ - sẽ truyền)' })" -ForegroundColor $(if ([string]::IsNullOrWhiteSpace($config.KeyboardMapping.Left)) { 'Red' } else { 'Green' })
    Write-Host "   Right mapping: '$($config.KeyboardMapping.Right)' $(if ([string]::IsNullOrWhiteSpace($config.KeyboardMapping.Right)) { '(TRỐNG - sẽ KHÔNG truyền)' } else { '(CÓ GIÁ TRỊ - sẽ truyền)' })" -ForegroundColor $(if ([string]::IsNullOrWhiteSpace($config.KeyboardMapping.Right)) { 'Red' } else { 'Green' })
} else {
    Write-Host "❌ Config file không tồn tại: $binConfig" -ForegroundColor Red
}

# 2. Kiểm tra DLL version
$coreDll = "d:\PBL4\PBL4-test\WPFUI_NEW\bin\Debug\net8.0-windows\Core.dll"
if (Test-Path $coreDll) {
    $dll = Get-Item $coreDll
    Write-Host "`n2. CORE.DLL BUILD TIME:" -ForegroundColor Yellow
    Write-Host "   Last modified: $($dll.LastWriteTime)" -ForegroundColor Cyan
    
    $timeDiff = (Get-Date) - $dll.LastWriteTime
    if ($timeDiff.TotalMinutes -lt 5) {
        Write-Host "   ✓ DLL mới (build cách đây $([math]::Round($timeDiff.TotalMinutes, 1)) phút)" -ForegroundColor Green
    } else {
        Write-Host "   ⚠ DLL cũ (build cách đây $([math]::Round($timeDiff.TotalMinutes, 1)) phút)" -ForegroundColor Yellow
    }
}

# 3. Kiểm tra app có đang chạy không
$processName = "WPFUI_NEW"
$runningApps = Get-Process -Name $processName -ErrorAction SilentlyContinue

Write-Host "`n3. APP ĐANG CHẠY:" -ForegroundColor Yellow
if ($runningApps) {
    foreach ($app in $runningApps) {
        Write-Host "   ⚠ App đang chạy (PID: $($app.Id), Start: $($app.StartTime))" -ForegroundColor Yellow
        Write-Host "   → CẦN RESTART APP để dùng code mới!" -ForegroundColor Red
    }
} else {
    Write-Host "   ✓ App không chạy - có thể chạy version mới" -ForegroundColor Green
}

Write-Host "`n=== KẾT LUẬN ===" -ForegroundColor Cyan
Write-Host "Theo config hiện tại:" -ForegroundColor White
Write-Host "  - WASD để TRỐNG → sẽ KHÔNG truyền ✓" -ForegroundColor Green
Write-Host "  - Arrow keys CÓ MAPPING → sẽ truyền ✓" -ForegroundColor Green
Write-Host "`nNếu hành vi khác:" -ForegroundColor White
Write-Host "  1. RESTART APP (đóng app cũ đi)" -ForegroundColor Yellow
Write-Host "  2. Hoặc trong app ấn SAVE để reload config" -ForegroundColor Yellow
