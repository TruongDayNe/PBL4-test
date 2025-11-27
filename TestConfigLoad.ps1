# Test script to verify config loading
Write-Host "=== Testing Config Load ===" -ForegroundColor Cyan

# Navigate to output folder
Set-Location "D:\PBL4\PBL4-test\WPFUI_NEW\bin\Debug\net8.0-windows"

# Check if keymapping.json exists
if (Test-Path "keymapping.json") {
    Write-Host "✓ keymapping.json found" -ForegroundColor Green
    
    # Read and display content
    $json = Get-Content "keymapping.json" | ConvertFrom-Json
    Write-Host "`nKeyboard Mapping:" -ForegroundColor Yellow
    $json.KeyboardMapping | Format-Table -AutoSize
    
    Write-Host "`nExpected: W → Z" -ForegroundColor Cyan
    Write-Host "Actual:   W → $($json.KeyboardMapping.W)" -ForegroundColor $(if ($json.KeyboardMapping.W -eq "Z") { "Green" } else { "Red" })
} else {
    Write-Host "❌ keymapping.json NOT FOUND" -ForegroundColor Red
}

# Check DLL versions
Write-Host "`n=== DLL Versions ===" -ForegroundColor Cyan
$dlls = @(
    "System.Text.Json.dll",
    "Microsoft.Bcl.AsyncInterfaces.dll",
    "Core.dll"
)

foreach ($dll in $dlls) {
    if (Test-Path $dll) {
        $version = [Reflection.AssemblyName]::GetAssemblyName("$PWD\$dll").Version
        Write-Host "$dll : $version" -ForegroundColor Green
    } else {
        Write-Host "$dll : NOT FOUND" -ForegroundColor Red
    }
}

Write-Host "`n=== Ready to test ===" -ForegroundColor Cyan
Write-Host "Now run WPFUI_NEW.exe and check console output"
