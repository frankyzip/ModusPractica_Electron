# ============================================================================
# ModusPractica - Pre-Upload Check Script
# Checks if all required files exist before deployment
# ============================================================================

Write-Host "`n=== ModusPractica Pre-Upload Check ===`n" -ForegroundColor Cyan

$allGood = $true
$requiredFiles = @(
    "moduspractica-app.html",
    "moduspractica-app.js",
    "moduspractica-dashboard.html",
    "moduspractica-dashboard.js",
    "moduspractica-practice-session.html",
    "moduspractica-practice-session.js",
    "EbbinghausEngine.js",
    "AdaptiveTauManager.js",
    "MemoryStabilityManager.js",
    "PersonalizedMemoryCalibration.js",
    "IntensityModule.js",
    "moduspractica-manual.html",
    "moduspractica-release-notes.html"
)

foreach ($file in $requiredFiles) {
    if (Test-Path $file) {
        $size = [math]::Round((Get-Item $file).Length / 1KB, 1)
        Write-Host "   [OK] $file ($size KB)" -ForegroundColor Green
    } else {
        Write-Host "   [!!] $file - MISSING!" -ForegroundColor Red
        $allGood = $false
    }
}

# Check shared styles
if (Test-Path "../styles.css") {
    Write-Host "   [OK] ../styles.css (Shared)" -ForegroundColor Green
} else {
    Write-Host "   [!!] ../styles.css - MISSING!" -ForegroundColor Red
    $allGood = $false
}

Write-Host ""
if ($allGood) {
    Write-Host "✅ All checks passed! Ready for upload." -ForegroundColor Green
} else {
    Write-Host "❌ Some files are missing. Please check before uploading." -ForegroundColor Red
}
