using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Scripts.Common.Models;
using UnityEditor;
using UnityEngine;

namespace Assets.Editor.Migration
{
    /// <summary>
    /// Migration tools for converting level data from copy-paste format to reference-based format.
    /// </summary>
    public static class LevelFormatMigration
    {
        private static readonly string LocationsPath = Path.Combine("Assets", "Content", "locations");
        private static readonly string TemplatesPath = Path.Combine(LocationsPath, "level_design_templates", "levels");
        private static readonly string PatternsCollectionFileName = "PatternsCollection.json";
        private static readonly string ThemeFileName = "obstacle_sprite_to_type_mappings.json";

        #region Migrate PatternsCollection

        [MenuItem("Tools/Migration/1. Migrate PatternsCollection")]
        public static void MigratePatternsCollectionMenu()
        {
            var path = Path.Combine(TemplatesPath, PatternsCollectionFileName);
            var fullPath = Path.GetFullPath(path);

            if (!File.Exists(fullPath))
            {
                Debug.LogError($"[Migration] PatternsCollection.json not found at {fullPath}");
                return;
            }

            var json = File.ReadAllText(fullPath);
            var oldData = JsonUtility.FromJson<LevelInfo>(json);

            var newCollection = MigratePatternsCollection(oldData);

            // Backup
            var backupPath = fullPath.Replace(".json", "_backup.json");
            File.Copy(fullPath, backupPath, true);
            Debug.Log($"[Migration] Backup saved to {backupPath}");

            // Save new format
            var newJson = JsonUtility.ToJson(newCollection, true);
            File.WriteAllText(fullPath, newJson);
            Debug.Log($"[Migration] PatternsCollection migrated: {newCollection.patterns.Count} patterns");

            AssetDatabase.Refresh();
        }

        public static PatternsCollection MigratePatternsCollection(LevelInfo oldData)
        {
            var collection = new PatternsCollection();

            foreach (var pattern in oldData.patterns)
            {
                var template = new PatternTemplate
                {
                    name = pattern.name,
                    description = pattern.desсription ?? ""
                };

                int id = 0;
                foreach (var obstacle in pattern.obstacles)
                {
                    template.obstacles.Add(new ObstacleSlot
                    {
                        id = id,
                        type = obstacle.type,
                        x = obstacle.x,
                        y = obstacle.y
                    });
                    id++;
                }

                template.nextObstacleId = id;
                collection.patterns.Add(template);
            }

            return collection;
        }

        #endregion

        #region Migrate Location Themes

        [MenuItem("Tools/Migration/2. Migrate Location Themes")]
        public static void MigrateLocationThemesMenu()
        {
            var locationsDir = Path.GetFullPath(LocationsPath);
            var dirs = Directory.GetDirectories(locationsDir)
                .Where(d => !Path.GetFileName(d).Contains("level_design_templates"));

            foreach (var dir in dirs)
            {
                var themePath = Path.Combine(dir, ThemeFileName);
                if (!File.Exists(themePath)) continue;

                var json = File.ReadAllText(themePath);
                var theme = JsonUtility.FromJson<LocationTheme>(json);

                var migrated = MigrateLocationTheme(theme);

                // Backup
                var backupPath = themePath.Replace(".json", "_backup.json");
                File.Copy(themePath, backupPath, true);

                var newJson = JsonUtility.ToJson(migrated, true);
                File.WriteAllText(themePath, newJson);
                Debug.Log($"[Migration] Theme migrated: {Path.GetFileName(dir)}");
            }

            AssetDatabase.Refresh();
        }

        public static LocationTheme MigrateLocationTheme(LocationTheme theme)
        {
            foreach (var mapping in theme.obstacle_sprite_to_type_mappings)
            {
                if (string.IsNullOrEmpty(mapping.@default) && mapping.sprites.Count > 0)
                {
                    mapping.@default = mapping.sprites[0];
                }
            }

            return theme;
        }

        #endregion

        #region Migrate Level Files

        [MenuItem("Tools/Migration/3. Migrate Level Files")]
        public static void MigrateLevelFilesMenu()
        {
            // Load migrated patterns collection
            var patternsPath = Path.GetFullPath(Path.Combine(TemplatesPath, PatternsCollectionFileName));
            if (!File.Exists(patternsPath))
            {
                Debug.LogError("[Migration] Run 'Migrate PatternsCollection' first!");
                return;
            }

            var patternsJson = File.ReadAllText(patternsPath);
            var patternsCollection = JsonUtility.FromJson<PatternsCollection>(patternsJson);

            var locationsDir = Path.GetFullPath(LocationsPath);
            var locationDirs = Directory.GetDirectories(locationsDir)
                .Where(d => !Path.GetFileName(d).Contains("level_design_templates"));

            int migratedCount = 0;

            foreach (var locationDir in locationDirs)
            {
                var themePath = Path.Combine(locationDir, ThemeFileName);
                if (!File.Exists(themePath)) continue;

                var themeJson = File.ReadAllText(themePath);
                var theme = JsonUtility.FromJson<LocationTheme>(themeJson);

                var locationFolderName = Path.GetFileName(locationDir);
                var locationId = ExtractLocationId(locationFolderName);

                var levelsDir = Path.Combine(locationDir, "levels");
                if (!Directory.Exists(levelsDir)) continue;

                var levelFiles = Directory.GetFiles(levelsDir, "level_*.json", SearchOption.AllDirectories);

                foreach (var levelFile in levelFiles)
                {
                    var levelJson = File.ReadAllText(levelFile);
                    var oldLevel = JsonUtility.FromJson<LevelInfo>(levelJson);

                    var newLevel = MigrateLevelFile(oldLevel, patternsCollection, theme, locationId);

                    // Backup
                    var backupPath = levelFile.Replace(".json", "_old_format_backup.json");
                    File.Copy(levelFile, backupPath, true);

                    var newJson = JsonUtility.ToJson(newLevel, true);
                    File.WriteAllText(levelFile, newJson);
                    migratedCount++;
                    Debug.Log($"[Migration] Level migrated: {levelFile}");
                }
            }

            Debug.Log($"[Migration] Total levels migrated: {migratedCount}");
            AssetDatabase.Refresh();
        }

        public static LevelInfoRef MigrateLevelFile(
            LevelInfo oldLevel,
            PatternsCollection patternsCollection,
            LocationTheme theme,
            string locationId)
        {
            var patternLookup = new Dictionary<string, PatternTemplate>(StringComparer.OrdinalIgnoreCase);
            foreach (var pt in patternsCollection.patterns)
                patternLookup[pt.name] = pt;

            var themeLookup = new Dictionary<int, SpriteTypeMapping>();
            foreach (var m in theme.obstacle_sprite_to_type_mappings)
                themeLookup[m.type] = m;

            var result = new LevelInfoRef
            {
                skyTexture = oldLevel.skyTexture,
                background2Texture = oldLevel.background2Texture,
                backgroundTexture = oldLevel.backgroundTexture,
                roadTexture = oldLevel.roadTexture,
                location = locationId,
                decorationPatterns = oldLevel.decorationPatterns ?? new List<DecorationPattern>()
            };

            foreach (var pattern in oldLevel.patterns)
            {
                var patternRef = new PatternRef
                {
                    @ref = pattern.name,
                    spriteSeed = 0
                };

                if (patternLookup.TryGetValue(pattern.name, out var template))
                {
                    // Match obstacles by position (type + x + y) to find corresponding slot ids
                    foreach (var obstacle in pattern.obstacles)
                    {
                        var matchingSlot = FindMatchingSlot(template, obstacle);
                        if (matchingSlot == null) continue;

                        // Check if sprite differs from default
                        var defaultSprite = GetDefaultSprite(obstacle.type, themeLookup);
                        if (defaultSprite != null &&
                            !string.Equals(obstacle.spriteName, defaultSprite, StringComparison.OrdinalIgnoreCase))
                        {
                            patternRef.overrides.Add(new SpriteOverride
                            {
                                obstacleId = matchingSlot.id,
                                spriteName = obstacle.spriteName
                            });
                        }
                    }
                }
                else
                {
                    Debug.LogWarning($"[Migration] Pattern '{pattern.name}' not found in PatternsCollection. Reference will be saved but may fail at resolve time.");
                }

                result.patternSequence.Add(patternRef);
            }

            return result;
        }

        private static ObstacleSlot FindMatchingSlot(PatternTemplate template, ObstacleModel obstacle)
        {
            return template.obstacles.FirstOrDefault(s =>
                s.type == obstacle.type &&
                Math.Abs(s.x - obstacle.x) < 0.01f &&
                Math.Abs(s.y - obstacle.y) < 0.01f);
        }

        private static string GetDefaultSprite(int type, Dictionary<int, SpriteTypeMapping> themeLookup)
        {
            if (themeLookup.TryGetValue(type, out var mapping))
            {
                return mapping.@default ?? (mapping.sprites.Count > 0 ? mapping.sprites[0] : null);
            }

            // Universal names for collectables
            return (ObstacleTypeEnum)type switch
            {
                ObstacleTypeEnum.collectableEnergetic => "energetic",
                ObstacleTypeEnum.collectablePizza => "pizza",
                ObstacleTypeEnum.collectableCrystal => "crystal",
                ObstacleTypeEnum.collectableLife => "life",
                ObstacleTypeEnum.collectableCoin => "coin",
                _ => null
            };
        }

        private static string ExtractLocationId(string folderName)
        {
            // "01_New_York" -> "new_york"
            var parts = folderName.Split('_');
            if (parts.Length > 1 && int.TryParse(parts[0], out _))
            {
                return string.Join("_", parts.Skip(1)).ToLower();
            }

            return folderName.ToLower();
        }

        #endregion
    }
}
