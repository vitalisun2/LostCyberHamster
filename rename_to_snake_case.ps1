# Script to rename bigAlive files to big_alive (snake_case)
# Run this script in the folder containing PNG files

Write-Host "=== Renaming bigAlive -> big_alive ===" -ForegroundColor Cyan
Write-Host ""

# Get current directory
$currentPath = Get-Location

Write-Host "Searching in: $currentPath" -ForegroundColor Yellow
Write-Host ""

# Find all files with bigAlive in name
$files = Get-ChildItem -Path $currentPath -Filter "*bigAlive*" -File

if ($files.Count -eq 0) {
    Write-Host "No files found with 'bigAlive' pattern." -ForegroundColor Red
    Write-Host ""
    Write-Host "Please navigate to the correct folder and run again:" -ForegroundColor Yellow
    Write-Host "  cd 'path\to\dropbox\obstacle_animations\new_york'" -ForegroundColor Gray
    Write-Host "  .\rename_to_snake_case.ps1" -ForegroundColor Gray
    exit
}

Write-Host "Found $($files.Count) file(s) to rename:" -ForegroundColor Green
$files | ForEach-Object { Write-Host "  - $($_.Name)" -ForegroundColor Gray }
Write-Host ""

# Ask for confirmation
$confirm = Read-Host "Proceed with renaming? (y/n)"
if ($confirm -ne 'y' -and $confirm -ne 'Y') {
    Write-Host "Cancelled." -ForegroundColor Yellow
    exit
}

Write-Host ""
Write-Host "Renaming files..." -ForegroundColor Cyan

# Rename each file
$renamed = 0
foreach ($file in $files) {
    $newName = $file.Name -replace 'bigAlive', 'big_alive'
    
    if ($file.Name -eq $newName) {
        Write-Host "  [SKIP] $($file.Name) - no change needed" -ForegroundColor Yellow
        continue
    }
    
    try {
        Rename-Item -Path $file.FullName -NewName $newName -ErrorAction Stop
        Write-Host "  [OK] $($file.Name) -> $newName" -ForegroundColor Green
        $renamed++
    }
    catch {
        Write-Host "  [ERROR] Failed to rename $($file.Name): $_" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "=== Complete ===" -ForegroundColor Cyan
Write-Host "Renamed: $renamed file(s)" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Open Unity" -ForegroundColor Gray
Write-Host "  2. Run: Tools -> Obstacle Animations -> Import From Dropbox" -ForegroundColor Gray
Write-Host "  3. This will re-import sprites with new snake_case names" -ForegroundColor Gray
