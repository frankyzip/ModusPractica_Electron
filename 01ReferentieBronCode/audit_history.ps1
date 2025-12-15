# Practice History Data Audit Script
$data = Get-Content "c:\Users\Frank\AppData\Roaming\ModusPractica\Profiles\Default\History\practice_history.json" -Raw | ConvertFrom-Json

Write-Host "=== PRACTICE HISTORY DATA AUDIT REPORT ===" -ForegroundColor Cyan
Write-Host "Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')`n"

# 1. BASIC STATS
Write-Host "1. BASIC STATISTICS" -ForegroundColor Yellow
Write-Host "  Total Records: $($data.Count)"
Write-Host "  Deleted: $(($data | Where-Object {$_.IsDeleted}).Count)"
Write-Host "  Active: $(($data | Where-Object {-not $_.IsDeleted}).Count)"

# 2. DATA QUALITY
Write-Host "`n2. DATA QUALITY CHECKS" -ForegroundColor Yellow
$negReps = ($data | Where-Object {$_.Repetitions -lt 0}).Count
Write-Host "  Negative Repetitions: $negReps $(if($negReps -gt 0){'⚠️ ERROR'} else {'✅ OK'})"

$zeroReps = ($data | Where-Object {$_.Repetitions -eq 0}).Count
Write-Host "  Zero Repetitions: $zeroReps ($(if($zeroReps -gt 0){[math]::Round($zeroReps/$data.Count*100,1)} else {0})%)"

$nullDiff = ($data | Where-Object {$null -eq $_.Difficulty}).Count
Write-Host "  Null Difficulty: $nullDiff $(if($nullDiff -gt 0){'⚠️ WARNING'} else {'✅ OK'})"

$sightReading = ($data | Where-Object {$_.IsSightReading}).Count
Write-Host "  Sight-Reading: $sightReading ($([math]::Round($sightReading/$data.Count*100,1))%)"

# 3. PERFORMANCE SCORES
Write-Host "`n3. PERFORMANCE SCORES" -ForegroundColor Yellow
$scores = $data | Where-Object {$_.PerformanceScore -gt 0} | Select-Object -ExpandProperty PerformanceScore
Write-Host "  Min: $($scores | Measure-Object -Minimum | Select-Object -ExpandProperty Minimum)"
Write-Host "  Max: $($scores | Measure-Object -Maximum | Select-Object -ExpandProperty Maximum)"
Write-Host "  Avg: $([math]::Round(($scores | Measure-Object -Average | Select-Object -ExpandProperty Average), 2))"

$excellent = ($data | Where-Object {$_.PerformanceScore -ge 9}).Count
$good = ($data | Where-Object {$_.PerformanceScore -ge 7 -and $_.PerformanceScore -lt 9}).Count
$average = ($data | Where-Object {$_.PerformanceScore -ge 5 -and $_.PerformanceScore -lt 7}).Count
$poor = ($data | Where-Object {$_.PerformanceScore -lt 5 -and $_.PerformanceScore -gt 0}).Count

Write-Host "  Excellent (≥9): $excellent ($([math]::Round($excellent/$data.Count*100,1))%)"
Write-Host "  Good (7-9): $good ($([math]::Round($good/$data.Count*100,1))%)"
Write-Host "  Average (5-7): $average ($([math]::Round($average/$data.Count*100,1))%)"
Write-Host "  Poor (<5): $poor ($([math]::Round($poor/$data.Count*100,1))%)"

# 4. SESSION OUTCOMES
Write-Host "`n4. SESSION OUTCOMES" -ForegroundColor Yellow
$data | Group-Object SessionOutcome | Sort-Object Count -Descending | ForEach-Object {
    $pct = [math]::Round($_.Count / $data.Count * 100, 1)
    Write-Host "  $($_.Name): $($_.Count) ($pct%)"
}

# 5. DIFFICULTY DISTRIBUTION
Write-Host "`n5. DIFFICULTY DISTRIBUTION" -ForegroundColor Yellow
$data | Where-Object {$_.Difficulty} | Group-Object Difficulty | Sort-Object Count -Descending | ForEach-Object {
    $pct = [math]::Round($_.Count / $data.Count * 100, 1)
    Write-Host "  $($_.Name): $($_.Count) ($pct%)"
}

# 6. LEARNING ZONES
Write-Host "`n6. LEARNING ZONES" -ForegroundColor Yellow
$data | Group-Object LearningZone | Sort-Object Count -Descending | ForEach-Object {
    $pct = [math]::Round($_.Count / $data.Count * 100, 1)
    Write-Host "  $($_.Name): $($_.Count) ($pct%)"
}

# 7. USER OVERRIDES
Write-Host "`n7. USER OVERRIDES" -ForegroundColor Yellow
$overrides = ($data | Where-Object {$_.IsUserOverride}).Count
Write-Host "  User Overrides: $overrides ($(if($overrides -gt 0){[math]::Round($overrides/$data.Count*100,1)} else {0})%)"

# 8. DATE RANGE
Write-Host "`n8. DATE RANGE" -ForegroundColor Yellow
$dates = $data | Select-Object -ExpandProperty Date | ForEach-Object {[DateTime]$_}
$firstDate = ($dates | Measure-Object -Minimum).Minimum
$lastDate = ($dates | Measure-Object -Maximum).Maximum
$span = ($lastDate - $firstDate).Days

Write-Host "  First Session: $($firstDate.ToString('yyyy-MM-dd HH:mm'))"
Write-Host "  Last Session: $($lastDate.ToString('yyyy-MM-dd HH:mm'))"
Write-Host "  Time Span: $span days"
Write-Host "  Avg Sessions/Day: $([math]::Round($data.Count / [Math]::Max($span, 1), 1))"

# 9. TOP MUSIC PIECES
Write-Host "`n9. TOP 5 PRACTICED PIECES" -ForegroundColor Yellow
$data | Group-Object MusicPieceTitle | Sort-Object Count -Descending | Select-Object -First 5 | ForEach-Object {
    $pct = [math]::Round($_.Count / $data.Count * 100, 1)
    Write-Host "  $($_.Name): $($_.Count) ($pct%)"
}

# 10. ISSUES FOUND
Write-Host "`n10. ISSUES & RECOMMENDATIONS" -ForegroundColor Yellow
if ($negReps -gt 0) {
    Write-Host "  ❌ CRITICAL: $negReps sessions with negative repetitions!" -ForegroundColor Red
}
if ($nullDiff -gt 0) {
    Write-Host "  ⚠️  WARNING: $nullDiff sessions without difficulty rating" -ForegroundColor DarkYellow
}
if ($poor -gt $data.Count * 0.2) {
    Write-Host "  ⚠️  WARNING: High number of poor performances (>20%)" -ForegroundColor DarkYellow
}
if ($negReps -eq 0 -and $nullDiff -le 2 -and $poor -le $data.Count * 0.15) {
    Write-Host "  ✅ DATA QUALITY: EXCELLENT - No major issues found!" -ForegroundColor Green
}

Write-Host "`n=== END OF REPORT ===" -ForegroundColor Cyan
