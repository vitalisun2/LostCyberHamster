using System.Collections.Generic;
using Assets.Scripts.Common.Models;
using Assets.Scripts.System.LevelManagement;
using NUnit.Framework;
using UnityEngine;

namespace Assets.Tests.EditMode
{
    public class LevelLoadingIntegrationTests
    {
        #region Helpers

        private static PatternsCollection CreatePatterns(params PatternTemplate[] templates)
        {
            return new PatternsCollection { patterns = new List<PatternTemplate>(templates) };
        }

        private static PatternTemplate CreateTemplate(string name, params ObstacleSlot[] slots)
        {
            return new PatternTemplate
            {
                name = name,
                description = $"test pattern {name}",
                nextObstacleId = slots.Length > 0 ? slots[slots.Length - 1].id + 1 : 0,
                obstacles = new List<ObstacleSlot>(slots)
            };
        }

        private static ObstacleSlot Slot(int id, int type, float x = 0f, float y = -2.8f)
        {
            return new ObstacleSlot { id = id, type = type, x = x, y = y };
        }

        private static LocationTheme CreateTheme(params SpriteTypeMapping[] mappings)
        {
            return new LocationTheme
            {
                obstacle_sprite_to_type_mappings = new List<SpriteTypeMapping>(mappings)
            };
        }

        private static SpriteTypeMapping Mapping(int type, string defaultSprite, params string[] extras)
        {
            var sprites = new List<string> { defaultSprite };
            sprites.AddRange(extras);
            return new SpriteTypeMapping
            {
                type = type,
                sprites = sprites,
                @default = defaultSprite
            };
        }

        #endregion

        [Test]
        public void LoadResolvedLevel_MatchesOldFormat()
        {
            // Simulate old-format LevelInfo
            var oldLevelInfo = new LevelInfo
            {
                skyTexture = "sky_ny",
                backgroundTexture = "bg_ny",
                background2Texture = "bg2_ny",
                roadTexture = "road_ny",
                patterns = new List<Pattern>
                {
                    new Pattern
                    {
                        name = "easy_run",
                        desсription = "easy pattern",
                        obstacles = new List<ObstacleModel>
                        {
                            new ObstacleModel { type = 0, x = 5f, y = -2.8f, spriteName = "obstacle_ny_smallAlive_1" },
                            new ObstacleModel { type = 1, x = 10f, y = -2.8f, spriteName = "obstacle_ny_bigAlive_2" }
                        }
                    }
                },
                decorationPatterns = new List<DecorationPattern>()
            };

            // Build equivalent ref-based data
            var patterns = CreatePatterns(
                CreateTemplate("easy_run",
                    Slot(0, 0, 5f, -2.8f),
                    Slot(1, 1, 10f, -2.8f)));

            var theme = CreateTheme(
                Mapping(0, "obstacle_ny_smallAlive_1"),
                Mapping(1, "obstacle_ny_bigAlive_2"));

            var levelRef = new LevelInfoRef
            {
                skyTexture = "sky_ny",
                backgroundTexture = "bg_ny",
                background2Texture = "bg2_ny",
                roadTexture = "road_ny",
                location = "01_New_York",
                patternSequence = new List<PatternRef>
                {
                    new PatternRef { @ref = "easy_run", spriteSeed = 0, overrides = new List<SpriteOverride>() }
                },
                decorationPatterns = new List<DecorationPattern>()
            };

            // Resolve
            var resolved = LevelResolver.Resolve(levelRef, patterns, theme);

            // Assert — positions, types, sprites match
            Assert.AreEqual(oldLevelInfo.skyTexture, resolved.skyTexture);
            Assert.AreEqual(oldLevelInfo.backgroundTexture, resolved.backgroundTexture);
            Assert.AreEqual(oldLevelInfo.roadTexture, resolved.roadTexture);
            Assert.AreEqual(oldLevelInfo.patterns.Count, resolved.patterns.Count);

            for (int p = 0; p < oldLevelInfo.patterns.Count; p++)
            {
                var oldPattern = oldLevelInfo.patterns[p];
                var newPattern = resolved.patterns[p];

                Assert.AreEqual(oldPattern.name, newPattern.name);
                Assert.AreEqual(oldPattern.obstacles.Count, newPattern.obstacles.Count);

                for (int o = 0; o < oldPattern.obstacles.Count; o++)
                {
                    var oldOb = oldPattern.obstacles[o];
                    var newOb = newPattern.obstacles[o];

                    Assert.AreEqual(oldOb.type, newOb.type, $"Pattern {p}, obstacle {o}: type mismatch");
                    Assert.AreEqual(oldOb.x, newOb.x, 0.001f, $"Pattern {p}, obstacle {o}: x mismatch");
                    Assert.AreEqual(oldOb.y, newOb.y, 0.001f, $"Pattern {p}, obstacle {o}: y mismatch");
                    Assert.AreEqual(oldOb.spriteName, newOb.spriteName, $"Pattern {p}, obstacle {o}: spriteName mismatch");
                }
            }
        }

        [Test]
        public void RoundTrip_SaveAndLoad_Preserves()
        {
            var levelRef = new LevelInfoRef
            {
                skyTexture = "sky_paris",
                backgroundTexture = "bg_paris",
                background2Texture = "bg2_paris",
                roadTexture = "road_paris",
                location = "02_Paris",
                patternSequence = new List<PatternRef>
                {
                    new PatternRef
                    {
                        @ref = "medium_diff",
                        spriteSeed = 42,
                        overrides = new List<SpriteOverride>
                        {
                            new SpriteOverride { obstacleId = 3, spriteName = "obstacle_paris_custom" }
                        }
                    },
                    new PatternRef
                    {
                        @ref = "easy_run",
                        spriteSeed = 0,
                        overrides = new List<SpriteOverride>()
                    }
                },
                decorationPatterns = new List<DecorationPattern>()
            };

            // Serialize
            var json = JsonUtility.ToJson(levelRef, true);

            // Deserialize
            var loaded = JsonUtility.FromJson<LevelInfoRef>(json);

            // Assert round-trip preserves all data
            Assert.AreEqual(levelRef.skyTexture, loaded.skyTexture);
            Assert.AreEqual(levelRef.location, loaded.location);
            Assert.AreEqual(levelRef.patternSequence.Count, loaded.patternSequence.Count);

            Assert.AreEqual("medium_diff", loaded.patternSequence[0].@ref);
            Assert.AreEqual(42, loaded.patternSequence[0].spriteSeed);
            Assert.AreEqual(1, loaded.patternSequence[0].overrides.Count);
            Assert.AreEqual(3, loaded.patternSequence[0].overrides[0].obstacleId);
            Assert.AreEqual("obstacle_paris_custom", loaded.patternSequence[0].overrides[0].spriteName);

            Assert.AreEqual("easy_run", loaded.patternSequence[1].@ref);
            Assert.AreEqual(0, loaded.patternSequence[1].spriteSeed);
        }

        [Test]
        public void RoundTrip_ResolvedLevelHasCorrectOverrides()
        {
            var patterns = CreatePatterns(
                CreateTemplate("test_pattern",
                    Slot(0, 0, 1f),
                    Slot(1, 0, 5f),
                    Slot(2, 1, 10f)));

            var theme = CreateTheme(
                Mapping(0, "default_small", "alt_small_1", "alt_small_2"),
                Mapping(1, "default_big", "alt_big_1"));

            var levelRef = new LevelInfoRef
            {
                skyTexture = "sky",
                backgroundTexture = "bg",
                background2Texture = "bg2",
                roadTexture = "road",
                location = "test",
                patternSequence = new List<PatternRef>
                {
                    new PatternRef
                    {
                        @ref = "test_pattern",
                        spriteSeed = 0,
                        overrides = new List<SpriteOverride>
                        {
                            new SpriteOverride { obstacleId = 1, spriteName = "alt_small_2" }
                        }
                    }
                }
            };

            // Serialize + deserialize (round-trip)
            var json = JsonUtility.ToJson(levelRef, true);
            var reloaded = JsonUtility.FromJson<LevelInfoRef>(json);

            // Resolve
            var resolved = LevelResolver.Resolve(reloaded, patterns, theme);

            Assert.AreEqual(1, resolved.patterns.Count);
            var obstacles = resolved.patterns[0].obstacles;
            Assert.AreEqual(3, obstacles.Count);

            // Obstacle 0: no override, seed=0 → default
            Assert.AreEqual("default_small", obstacles[0].spriteName);
            // Obstacle 1: manual override
            Assert.AreEqual("alt_small_2", obstacles[1].spriteName);
            // Obstacle 2: no override, seed=0 → default
            Assert.AreEqual("default_big", obstacles[2].spriteName);
        }
    }
}
