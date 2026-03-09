<#
.SYNOPSIS
    Migrates level data from old copy-paste format to new reference-based format.
    Equivalent to LevelFormatMigration.cs menu items but runs outside Unity.
#>

$ErrorActionPreference = 'Stop'

$LocationsPath = "c:\Personal\crystal-wave\repos\LostCyberHamster_2025\LostCyberHamster\Assets\Content\Locations"
$TemplatesPath = Join-Path $LocationsPath "level_design_templates\levels"
$PatternsCollectionFile = Join-Path $TemplatesPath "PatternsCollection.json"

# ============================================================
# Step 1: Migrate PatternsCollection
# ============================================================
Write-Host "`n=== Step 1: Migrate PatternsCollection ===" -ForegroundColor Cyan

$pcJson = Get-Content $PatternsCollectionFile -Raw | ConvertFrom-Json

# Backup
$backupPath = $PatternsCollectionFile.Replace(".json", "_backup.json")
Copy-Item $PatternsCollectionFile $backupPath -Force
Write-Host "Backup saved to $backupPath"

# Build new format
$newPatterns = @()
foreach ($pattern in $pcJson.patterns) {
    $newObstacles = @()
    $id = 0
    foreach ($obs in $pattern.obstacles) {
        $newObstacles += [ordered]@{
            id   = $id
            type = $obs.type
            x    = $obs.x
            y    = $obs.y
        }
        $id++
    }
    
    $desc = if ($null -ne $pattern.PSObject.Properties['desсription']) { $pattern.'desсription' } else { "" }
    
    $newPatterns += [ordered]@{
        name           = $pattern.name
        description    = $desc
        nextObstacleId = $id
        obstacles      = $newObstacles
    }
}

$newPC = [ordered]@{
    patterns = $newPatterns
}

$newPCJson = $newPC | ConvertTo-Json -Depth 10
[System.IO.File]::WriteAllText($PatternsCollectionFile, $newPCJson, [System.Text.Encoding]::UTF8)
Write-Host "PatternsCollection migrated: $($newPatterns.Count) patterns"

# ============================================================
# Step 2: Migrate Location Themes
# ============================================================
Write-Host "`n=== Step 2: Migrate Location Themes ===" -ForegroundColor Cyan

$locationDirs = Get-ChildItem $LocationsPath -Directory | Where-Object { 
    $_.Name -ne "level_design_templates" -and $_.Name -ne "previews"
}

foreach ($locDir in $locationDirs) {
    $themePath = Join-Path $locDir.FullName "obstacle_sprite_to_type_mappings.json"
    if (!(Test-Path $themePath)) { continue }
    
    $themeJson = Get-Content $themePath -Raw | ConvertFrom-Json
    
    # Backup
    $thBackup = $themePath.Replace(".json", "_backup.json")
    Copy-Item $themePath $thBackup -Force
    
    # Add default field to each mapping
    $newMappings = @()
    foreach ($m in $themeJson.obstacle_sprite_to_type_mappings) {
        $defaultSprite = if ($m.sprites.Count -gt 0) { $m.sprites[0] } else { "" }
        $newMappings += [ordered]@{
            type    = $m.type
            sprites = @($m.sprites)
            default = $defaultSprite
        }
    }
    
    $newTheme = [ordered]@{
        obstacle_sprite_to_type_mappings = $newMappings
    }
    
    $newThemeJson = $newTheme | ConvertTo-Json -Depth 10
    [System.IO.File]::WriteAllText($themePath, $newThemeJson, [System.Text.Encoding]::UTF8)
    Write-Host "Theme migrated: $($locDir.Name)"
}

# ============================================================
# Step 3: Migrate Level Files
# ============================================================
Write-Host "`n=== Step 3: Migrate Level Files ===" -ForegroundColor Cyan

# Reload migrated PatternsCollection
$pcMigrated = Get-Content $PatternsCollectionFile -Raw | ConvertFrom-Json
$patternLookup = @{}
foreach ($pt in $pcMigrated.patterns) {
    $patternLookup[$pt.name] = $pt
}

$migratedCount = 0

foreach ($locDir in $locationDirs) {
    $themePath = Join-Path $locDir.FullName "obstacle_sprite_to_type_mappings.json"
    if (!(Test-Path $themePath)) { continue }
    
    $theme = Get-Content $themePath -Raw | ConvertFrom-Json
    
    # Build theme lookup: type -> mapping
    $themeLookup = @{}
    foreach ($m in $theme.obstacle_sprite_to_type_mappings) {
        $themeLookup[$m.type] = $m
    }
    
    # Extract location ID (e.g. "01_New_York" -> "01_New_York")
    $locationId = $locDir.Name
    
    $levelsDir = Join-Path $locDir.FullName "levels"
    if (!(Test-Path $levelsDir)) { continue }
    
    $levelFiles = Get-ChildItem $levelsDir -Recurse -Filter "*.json" | Where-Object { $_.Name -notmatch "_backup" }
    
    foreach ($levelFile in $levelFiles) {
        $levelJson = Get-Content $levelFile.FullName -Raw | ConvertFrom-Json
        
        # Skip files that don't have patterns (already migrated or not level files)
        if ($null -eq $levelJson.patterns) { continue }
        
        # Backup
        $lvlBackup = $levelFile.FullName.Replace(".json", "_old_format_backup.json")
        Copy-Item $levelFile.FullName $lvlBackup -Force
        
        # Build patternSequence from old patterns
        $patternSequence = @()
        foreach ($pattern in $levelJson.patterns) {
            $patternRef = [ordered]@{
                ref       = $pattern.name
                spriteSeed = 0
                overrides  = @()
            }
            
            $template = $patternLookup[$pattern.name]
            if ($null -ne $template) {
                foreach ($obstacle in $pattern.obstacles) {
                    # Find matching slot by type + x + y
                    $matchingSlot = $null
                    foreach ($slot in $template.obstacles) {
                        if ($slot.type -eq $obstacle.type -and
                            [Math]::Abs($slot.x - $obstacle.x) -lt 0.01 -and
                            [Math]::Abs($slot.y - $obstacle.y) -lt 0.01) {
                            $matchingSlot = $slot
                            break
                        }
                    }
                    
                    if ($null -eq $matchingSlot) { continue }
                    
                    # Check if sprite differs from theme default
                    $defaultSprite = $null
                    $mapping = $themeLookup[$obstacle.type]
                    if ($null -ne $mapping) {
                        $defaultSprite = $mapping.default
                    }
                    else {
                        # Universal collectable names
                        $defaultSprite = switch ($obstacle.type) {
                            5  { "energetic" }
                            6  { "pizza" }
                            7  { "crystal" }
                            8  { "life" }
                            9  { "coin" }
                            default { $null }
                        }
                    }
                    
                    if ($null -ne $defaultSprite -and 
                        $obstacle.spriteName -ne $defaultSprite) {
                        $patternRef.overrides += [ordered]@{
                            obstacleId = $matchingSlot.id
                            spriteName = $obstacle.spriteName
                        }
                    }
                }
            }
            else {
                Write-Host "  WARNING: Pattern '$($pattern.name)' not found in PatternsCollection" -ForegroundColor Yellow
            }
            
            $patternSequence += $patternRef
        }
        
        # Build new level format
        $newLevel = [ordered]@{
            skyTexture         = $(if ($levelJson.skyTexture) { $levelJson.skyTexture } else { "" })
            background2Texture = $(if ($levelJson.background2Texture) { $levelJson.background2Texture } else { "" })
            backgroundTexture  = $(if ($levelJson.backgroundTexture) { $levelJson.backgroundTexture } else { "" })
            roadTexture        = $(if ($levelJson.roadTexture) { $levelJson.roadTexture } else { "" })
            location           = $locationId
            patternSequence    = $patternSequence
            decorationPatterns = @($(if ($levelJson.decorationPatterns) { $levelJson.decorationPatterns } else { @() }))
        }
        
        $newLevelJson = $newLevel | ConvertTo-Json -Depth 20
        [System.IO.File]::WriteAllText($levelFile.FullName, $newLevelJson, [System.Text.Encoding]::UTF8)
        $migratedCount++
        Write-Host "Level migrated: $($levelFile.Name)"
    }
}

Write-Host "`n=== Migration complete! ===" -ForegroundColor Green
Write-Host "Patterns: $($newPatterns.Count) templates"
Write-Host "Levels migrated: $migratedCount"
Write-Host "Backups created for all original files"
