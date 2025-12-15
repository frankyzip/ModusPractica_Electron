# ============================================================================
# ModusPractica - Version Update Script
# Updates version strings for cache busting in all HTML files
# ============================================================================

$newVersion = "20251125-1"
$targetDir = $PSScriptRoot
$files = Get-ChildItem -Path $targetDir -Filter "*.html" -Recurse

Write-Host "Updating version to $newVersion in $($files.Count) files..." -ForegroundColor Cyan

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    if ($content -match '\?v=\d{8}-\d+') {
        $newContent = $content -replace '\?v=\d{8}-\d+', "?v=$newVersion"
        if ($newContent -ne $content) {
            Set-Content -Path $file.FullName -Value $newContent -Encoding UTF8
            Write-Host "Updated: $($file.Name)" -ForegroundColor Green
        }
    }
}

Write-Host "Done!" -ForegroundColor Cyan
