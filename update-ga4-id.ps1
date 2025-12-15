# ============================================================================
# PowerShell Script: Update Google Analytics Measurement ID
# Voor ModusPractica GA4 implementatie
# ============================================================================

param(
    [Parameter(Mandatory = $true)]
    [string]$MeasurementID
)

# Valideer Measurement ID formaat
if ($MeasurementID -notmatch '^G-[A-Z0-9]{10}$') {
    Write-Host "❌ Error: Measurement ID moet het formaat G-XXXXXXXXXX hebben (bijv. G-ABC1234567)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Voorbeeld gebruik:" -ForegroundColor Yellow
    Write-Host "  .\update-ga4-id.ps1 -MeasurementID G-ABC1234567" -ForegroundColor Yellow
    exit 1
}

Write-Host "🔧 Google Analytics 4 Measurement ID Updater" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Measurement ID: $MeasurementID" -ForegroundColor Green
Write-Host ""

# Pad naar moduspractica folder
$ModusPracticaPath = Join-Path $PSScriptRoot "moduspractica"

# Lijst van HTML-bestanden die moeten worden bijgewerkt
$FilesToUpdate = @(
    "moduspractica-app.html",
    "moduspractica-dashboard.html",
    "moduspractica-practice-session.html",
    "moduspractica-piece-detail.html",
    "moduspractica-calendar.html",
    "moduspractica-statistics.html"
)

$UpdatedFiles = 0
$FailedFiles = @()

foreach ($FileName in $FilesToUpdate) {
    $FilePath = Join-Path $ModusPracticaPath $FileName
    
    if (Test-Path $FilePath) {
        try {
            # Lees bestand
            $Content = Get-Content $FilePath -Raw -Encoding UTF8
            
            # Check of bestand het placeholder ID bevat
            if ($Content -match 'G-XXXXXXXXXX') {
                # Vervang alle voorkomens van G-XXXXXXXXXX met het echte ID
                $UpdatedContent = $Content -replace 'G-XXXXXXXXXX', $MeasurementID
                
                # Schrijf terug
                Set-Content -Path $FilePath -Value $UpdatedContent -Encoding UTF8 -NoNewline
                
                Write-Host "✅ Updated: $FileName" -ForegroundColor Green
                $UpdatedFiles++
            }
            else {
                Write-Host "⏭️  Skipped: $FileName (geen placeholder gevonden)" -ForegroundColor Yellow
            }
        }
        catch {
            Write-Host "❌ Failed: $FileName - $($_.Exception.Message)" -ForegroundColor Red
            $FailedFiles += $FileName
        }
    }
    else {
        Write-Host "⚠️  Not found: $FileName" -ForegroundColor Yellow
        $FailedFiles += $FileName
    }
}

Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  Updated: $UpdatedFiles files" -ForegroundColor Green

if ($FailedFiles.Count -gt 0) {
    Write-Host "  Failed/Missing: $($FailedFiles.Count) files" -ForegroundColor Red
    Write-Host "    - $($FailedFiles -join "`n    - ")" -ForegroundColor Red
}

Write-Host ""
Write-Host "🎉 Measurement ID update completed!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Test op localhost: http://localhost:8080/moduspractica/moduspractica-app.html" -ForegroundColor White
Write-Host "  2. Check browser console (F12) voor '[GA4] Developer mode' bericht" -ForegroundColor White
Write-Host "  3. Deploy naar productie" -ForegroundColor White
Write-Host "  4. Test op productie in incognito mode" -ForegroundColor White
Write-Host "  5. Controleer Google Analytics Realtime dashboard" -ForegroundColor White
Write-Host ""
