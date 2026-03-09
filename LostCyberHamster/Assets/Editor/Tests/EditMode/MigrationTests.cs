using System.Collections.Generic;
using System.Linq;
using Assets.Editor.Migration;
using Assets.Scripts.Common.Models;
using NUnit.Framework;

namespace Assets.Tests.EditMode
{
    public class MigrationTests
    {
        #region MigratePatternsCollection

        [Test]
        public void MigratePatternsCollection_AssignsSequentialIds()
        {
            var oldData = new LevelInfo
            {
                patterns = new List<Pattern>
                {
                    new()
                    {
                        name = "test_pattern",
                        desсription = "test",
                        obstacles = new List<ObstacleModel>
                        {
                            new() { spriteName = "sprite_a", type = 0, x = 10f, y = -2.8f },
                            new() { spriteName = "sprite_b", type = 1, x = 20f, y = -2.8f },
                            new() { spriteName = "sprite_c", type = 2, x = 30f, y = -1.8f }
                        }
                    }
                }
            };

            var result = LevelFormatMigration.MigratePatternsCollection(oldData);

            var ids = result.patterns[0].obstacles.Select(o => o.id).ToList();
            Assert.AreEqual(new List<int> { 0, 1, 2 }, ids);
            Assert.AreEqual(3, result.patterns[0].nextObstacleId);
        }

        [Test]
        public void MigratePatternsCollection_PreservesTypeAndPosition()
        {
            var oldData = new LevelInfo
            {
                patterns = new List<Pattern>
                {
                    new()
                    {
                        name = "p",
                        obstacles = new List<ObstacleModel>
                        {
                            new() { spriteName = "s", type = 4, x = 22.2f, y = -2.8f }
                        }
                    }
                }
            };

            var result = LevelFormatMigration.MigratePatternsCollection(oldData);

            var slot = result.patterns[0].obstacles[0];
            Assert.AreEqual(4, slot.type);
            Assert.AreEqual(22.2f, slot.x, 0.001f);
            Assert.AreEqual(-2.8f, slot.y, 0.001f);
        }

        [Test]
        public void MigratePatternsCollection_PreservesNameAndDescription()
        {
            var oldData = new LevelInfo
            {
                patterns = new List<Pattern>
                {
                    new()
                    {
                        name = "easy_run",
                        desсription = "простой участок",
                        obstacles = new List<ObstacleModel>()
                    }
                }
            };

            var result = LevelFormatMigration.MigratePatternsCollection(oldData);

            Assert.AreEqual("easy_run", result.patterns[0].name);
            Assert.AreEqual("простой участок", result.patterns[0].description);
        }

        #endregion

        #region MigrateLocationTheme

        [Test]
        public void MigrateLocationTheme_AddsDefaultFromFirstSprite()
        {
            var theme = new LocationTheme
            {
                obstacle_sprite_to_type_mappings = new List<SpriteTypeMapping>
                {
                    new()
                    {
                        type = 0,
                        sprites = new List<string> { "dog", "cat" }
                        // default is null
                    }
                }
            };

            var result = LevelFormatMigration.MigrateLocationTheme(theme);

            Assert.AreEqual("dog", result.obstacle_sprite_to_type_mappings[0].@default);
        }

        [Test]
        public void MigrateLocationTheme_PreservesExistingDefault()
        {
            var theme = new LocationTheme
            {
                obstacle_sprite_to_type_mappings = new List<SpriteTypeMapping>
                {
                    new()
                    {
                        type = 0,
                        sprites = new List<string> { "dog", "cat" },
                        @default = "cat"
                    }
                }
            };

            var result = LevelFormatMigration.MigrateLocationTheme(theme);

            Assert.AreEqual("cat", result.obstacle_sprite_to_type_mappings[0].@default);
        }

        #endregion

        #region MigrateLevelFile

        [Test]
        public void MigrateLevel_PreservesDecorations()
        {
            var oldLevel = new LevelInfo
            {
                skyTexture = "sky",
                backgroundTexture = "bg",
                decorationPatterns = new List<DecorationPattern>
                {
                    new()
                    {
                        decorationTiles = new List<DecorationTile>
                        {
                            new() { name = "bush", xPos = 5, yPos = 10 }
                        }
                    }
                },
                patterns = new List<Pattern>()
            };

            var patterns = new PatternsCollection();
            var theme = new LocationTheme();

            var result = LevelFormatMigration.MigrateLevelFile(oldLevel, patterns, theme, "test");

            Assert.AreEqual(1, result.decorationPatterns.Count);
            Assert.AreEqual("bush", result.decorationPatterns[0].decorationTiles[0].name);
            Assert.AreEqual(5, result.decorationPatterns[0].decorationTiles[0].xPos);
        }

        [Test]
        public void MigrateLevel_DetectsOverrides()
        {
            var oldLevel = new LevelInfo
            {
                patterns = new List<Pattern>
                {
                    new()
                    {
                        name = "p",
                        obstacles = new List<ObstacleModel>
                        {
                            new() { spriteName = "granny", type = 1, x = 10f, y = -2.8f }
                        }
                    }
                }
            };

            var patternsCollection = new PatternsCollection
            {
                patterns = new List<PatternTemplate>
                {
                    new()
                    {
                        name = "p",
                        obstacles = new List<ObstacleSlot>
                        {
                            new() { id = 0, type = 1, x = 10f, y = -2.8f }
                        }
                    }
                }
            };

            var theme = new LocationTheme
            {
                obstacle_sprite_to_type_mappings = new List<SpriteTypeMapping>
                {
                    new()
                    {
                        type = 1,
                        sprites = new List<string> { "businessman", "granny" },
                        @default = "businessman"
                    }
                }
            };

            var result = LevelFormatMigration.MigrateLevelFile(oldLevel, patternsCollection, theme, "ny");

            Assert.AreEqual(1, result.patternSequence[0].overrides.Count);
            Assert.AreEqual(0, result.patternSequence[0].overrides[0].obstacleId);
            Assert.AreEqual("granny", result.patternSequence[0].overrides[0].spriteName);
        }

        [Test]
        public void MigrateLevel_NoOverridesWhenDefault()
        {
            var oldLevel = new LevelInfo
            {
                patterns = new List<Pattern>
                {
                    new()
                    {
                        name = "p",
                        obstacles = new List<ObstacleModel>
                        {
                            new() { spriteName = "businessman", type = 1, x = 10f, y = -2.8f },
                            new() { spriteName = "coin", type = 9, x = 20f, y = 1.2f }
                        }
                    }
                }
            };

            var patternsCollection = new PatternsCollection
            {
                patterns = new List<PatternTemplate>
                {
                    new()
                    {
                        name = "p",
                        obstacles = new List<ObstacleSlot>
                        {
                            new() { id = 0, type = 1, x = 10f, y = -2.8f },
                            new() { id = 1, type = 9, x = 20f, y = 1.2f }
                        }
                    }
                }
            };

            var theme = new LocationTheme
            {
                obstacle_sprite_to_type_mappings = new List<SpriteTypeMapping>
                {
                    new()
                    {
                        type = 1,
                        sprites = new List<string> { "businessman" },
                        @default = "businessman"
                    }
                }
            };

            var result = LevelFormatMigration.MigrateLevelFile(oldLevel, patternsCollection, theme, "ny");

            Assert.AreEqual(0, result.patternSequence[0].overrides.Count);
        }

        #endregion
    }
}
