using System.Collections.Generic;
using Assets.Scripts.Common.Models;
using Assets.Scripts.System.LevelManagement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Assets.Tests.EditMode
{
    public class LevelResolverTests
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
                nextObstacleId = slots.Length,
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

        private static LevelInfoRef CreateLevelRef(params PatternRef[] refs)
        {
            return new LevelInfoRef
            {
                skyTexture = "sky",
                roadTexture = "road",
                location = "new_york",
                patternSequence = new List<PatternRef>(refs),
                decorationPatterns = new List<DecorationPattern>()
            };
        }

        private static PatternRef Ref(string name, int seed = 0, params SpriteOverride[] overrides)
        {
            return new PatternRef
            {
                @ref = name,
                spriteSeed = seed,
                overrides = new List<SpriteOverride>(overrides)
            };
        }

        private static SpriteOverride Override(int obstacleId, string spriteName)
        {
            return new SpriteOverride { obstacleId = obstacleId, spriteName = spriteName };
        }

        #endregion

        #region Basic Resolution

        [Test]
        public void Resolve_SinglePattern_ResolvesDefaultSprites()
        {
            var patterns = CreatePatterns(
                CreateTemplate("easy_run",
                    Slot(0, 2),   // smallNotAliveRoad
                    Slot(1, 0))); // smallAlive

            var theme = CreateTheme(
                Mapping(2, "obstacle_manhole"),
                Mapping(0, "obstacle_dog"));

            var levelRef = CreateLevelRef(Ref("easy_run"));

            var result = LevelResolver.Resolve(levelRef, patterns, theme);

            Assert.AreEqual(1, result.patterns.Count);
            Assert.AreEqual(2, result.patterns[0].obstacles.Count);
            Assert.AreEqual("obstacle_manhole", result.patterns[0].obstacles[0].spriteName);
            Assert.AreEqual("obstacle_dog", result.patterns[0].obstacles[1].spriteName);
        }

        [Test]
        public void Resolve_CollectableTypes_ResolvedToUniversalNames()
        {
            var patterns = CreatePatterns(
                CreateTemplate("bonuses",
                    Slot(0, 5),   // energetic
                    Slot(1, 6),   // pizza
                    Slot(2, 7),   // crystal
                    Slot(3, 8),   // life
                    Slot(4, 9))); // coin

            var theme = CreateTheme(); // no mappings for collectables

            var levelRef = CreateLevelRef(Ref("bonuses"));

            var result = LevelResolver.Resolve(levelRef, patterns, theme);

            Assert.AreEqual("energetic", result.patterns[0].obstacles[0].spriteName);
            Assert.AreEqual("pizza", result.patterns[0].obstacles[1].spriteName);
            Assert.AreEqual("crystal", result.patterns[0].obstacles[2].spriteName);
            Assert.AreEqual("life", result.patterns[0].obstacles[3].spriteName);
            Assert.AreEqual("coin", result.patterns[0].obstacles[4].spriteName);
        }

        [Test]
        public void Resolve_MultiplePatterns_ResolvedInOrder()
        {
            var patterns = CreatePatterns(
                CreateTemplate("a", Slot(0, 0)),
                CreateTemplate("b", Slot(0, 0)),
                CreateTemplate("c", Slot(0, 0)));

            var theme = CreateTheme(Mapping(0, "sprite_default"));

            var levelRef = CreateLevelRef(Ref("a"), Ref("b"), Ref("c"));

            var result = LevelResolver.Resolve(levelRef, patterns, theme);

            Assert.AreEqual(3, result.patterns.Count);
            Assert.AreEqual("a", result.patterns[0].name);
            Assert.AreEqual("b", result.patterns[1].name);
            Assert.AreEqual("c", result.patterns[2].name);
        }

        #endregion

        #region Overrides

        [Test]
        public void Resolve_WithSpriteOverride_OverrideTakesPriority()
        {
            var patterns = CreatePatterns(
                CreateTemplate("p",
                    Slot(0, 0),
                    Slot(1, 0)));

            var theme = CreateTheme(Mapping(0, "default_sprite", "alt_sprite"));

            var levelRef = CreateLevelRef(
                Ref("p", 0, Override(1, "custom_sprite")));

            var result = LevelResolver.Resolve(levelRef, patterns, theme);

            Assert.AreEqual("default_sprite", result.patterns[0].obstacles[0].spriteName);
            Assert.AreEqual("custom_sprite", result.patterns[0].obstacles[1].spriteName);
        }

        [Test]
        public void Resolve_OverrideForNonexistentId_IgnoredWithoutError()
        {
            var patterns = CreatePatterns(
                CreateTemplate("p", Slot(0, 0)));

            var theme = CreateTheme(Mapping(0, "default_sprite"));

            var levelRef = CreateLevelRef(
                Ref("p", 0, Override(999, "ghost_sprite")));

            var result = LevelResolver.Resolve(levelRef, patterns, theme);

            Assert.AreEqual(1, result.patterns[0].obstacles.Count);
            Assert.AreEqual("default_sprite", result.patterns[0].obstacles[0].spriteName);
        }

        [Test]
        public void Resolve_MultipleOverridesInPattern_AllApplied()
        {
            var patterns = CreatePatterns(
                CreateTemplate("p",
                    Slot(0, 1),
                    Slot(1, 1),
                    Slot(2, 1)));

            var theme = CreateTheme(Mapping(1, "businessman", "granny", "hipster"));

            var levelRef = CreateLevelRef(
                Ref("p", 0,
                    Override(0, "granny"),
                    Override(2, "hipster")));

            var result = LevelResolver.Resolve(levelRef, patterns, theme);

            Assert.AreEqual("granny", result.patterns[0].obstacles[0].spriteName);
            Assert.AreEqual("businessman", result.patterns[0].obstacles[1].spriteName);
            Assert.AreEqual("hipster", result.patterns[0].obstacles[2].spriteName);
        }

        #endregion

        #region Seed-Based Randomness

        [Test]
        public void Resolve_WithSeed_DeterministicSpriteSelection()
        {
            var patterns = CreatePatterns(
                CreateTemplate("p", Slot(0, 1), Slot(1, 1), Slot(2, 1)));

            var theme = CreateTheme(Mapping(1, "a", "b", "c", "d", "e"));

            var levelRef = CreateLevelRef(Ref("p", 42));

            var result1 = LevelResolver.Resolve(levelRef, patterns, theme);
            var result2 = LevelResolver.Resolve(levelRef, patterns, theme);

            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(
                    result1.patterns[0].obstacles[i].spriteName,
                    result2.patterns[0].obstacles[i].spriteName,
                    $"Obstacle {i} should be deterministic");
            }
        }

        [Test]
        public void Resolve_DifferentSeeds_DifferentResults()
        {
            var patterns = CreatePatterns(
                CreateTemplate("p",
                    Slot(0, 1), Slot(1, 1), Slot(2, 1),
                    Slot(3, 1), Slot(4, 1), Slot(5, 1),
                    Slot(6, 1), Slot(7, 1), Slot(8, 1),
                    Slot(9, 1)));

            var theme = CreateTheme(Mapping(1, "a", "b", "c", "d", "e"));

            var levelRef1 = CreateLevelRef(Ref("p", 42));
            var levelRef2 = CreateLevelRef(Ref("p", 99));

            var result1 = LevelResolver.Resolve(levelRef1, patterns, theme);
            var result2 = LevelResolver.Resolve(levelRef2, patterns, theme);

            bool anyDifferent = false;
            for (int i = 0; i < 10; i++)
            {
                if (result1.patterns[0].obstacles[i].spriteName !=
                    result2.patterns[0].obstacles[i].spriteName)
                {
                    anyDifferent = true;
                    break;
                }
            }

            Assert.IsTrue(anyDifferent, "Different seeds should produce at least one different sprite across 10 obstacles");
        }

        [Test]
        public void Resolve_SeedZero_AlwaysDefault()
        {
            var patterns = CreatePatterns(
                CreateTemplate("p", Slot(0, 1), Slot(1, 1), Slot(2, 1)));

            var theme = CreateTheme(Mapping(1, "default_one", "alt_one", "alt_two"));

            var levelRef = CreateLevelRef(Ref("p", 0));

            var result = LevelResolver.Resolve(levelRef, patterns, theme);

            foreach (var obstacle in result.patterns[0].obstacles)
            {
                Assert.AreEqual("default_one", obstacle.spriteName);
            }
        }

        [Test]
        public void Resolve_SeedWithOverride_OverrideWins()
        {
            var patterns = CreatePatterns(
                CreateTemplate("p", Slot(0, 1), Slot(1, 1)));

            var theme = CreateTheme(Mapping(1, "a", "b", "c", "d", "e"));

            var levelRef = CreateLevelRef(
                Ref("p", 42, Override(0, "forced_sprite")));

            var result = LevelResolver.Resolve(levelRef, patterns, theme);

            Assert.AreEqual("forced_sprite", result.patterns[0].obstacles[0].spriteName);
        }

        [Test]
        public void Resolve_SeedWithSingleSprite_ReturnsDefault()
        {
            var patterns = CreatePatterns(
                CreateTemplate("p", Slot(0, 2)));

            var theme = CreateTheme(Mapping(2, "only_one"));

            var levelRef = CreateLevelRef(Ref("p", 42));

            var result = LevelResolver.Resolve(levelRef, patterns, theme);

            Assert.AreEqual("only_one", result.patterns[0].obstacles[0].spriteName);
        }

        #endregion

        #region Error Handling

        [Test]
        public void Resolve_PatternNotFound_SkipsPattern()
        {
            var patterns = CreatePatterns(
                CreateTemplate("existing", Slot(0, 0)));

            var theme = CreateTheme(Mapping(0, "sprite"));

            var levelRef = CreateLevelRef(
                Ref("nonexistent"),
                Ref("existing"));

            LogAssert.Expect(LogType.Error, "[LevelResolver] Pattern 'nonexistent' not found in PatternsCollection. Skipping.");
            var result = LevelResolver.Resolve(levelRef, patterns, theme);

            Assert.AreEqual(1, result.patterns.Count);
            Assert.AreEqual("existing", result.patterns[0].name);
        }

        [Test]
        public void Resolve_TypeNotInTheme_UsesUniversalName()
        {
            var patterns = CreatePatterns(
                CreateTemplate("p", Slot(0, 9))); // coin

            var theme = CreateTheme(); // empty theme

            var levelRef = CreateLevelRef(Ref("p"));

            var result = LevelResolver.Resolve(levelRef, patterns, theme);

            Assert.AreEqual("coin", result.patterns[0].obstacles[0].spriteName);
        }

        [Test]
        public void Resolve_EmptyPatternSequence_ReturnsEmptyPatterns()
        {
            var patterns = CreatePatterns(CreateTemplate("p", Slot(0, 0)));
            var theme = CreateTheme(Mapping(0, "sprite"));
            var levelRef = CreateLevelRef();

            var result = LevelResolver.Resolve(levelRef, patterns, theme);

            Assert.AreEqual(0, result.patterns.Count);
        }

        #endregion

        #region Multiple Instances

        [Test]
        public void Resolve_SamePatternTwice_IndependentOverrides()
        {
            var patterns = CreatePatterns(
                CreateTemplate("p", Slot(0, 1), Slot(1, 1)));

            var theme = CreateTheme(Mapping(1, "default_sprite", "alt_sprite"));

            var levelRef = CreateLevelRef(
                Ref("p", 0, Override(0, "override_a")),
                Ref("p", 0, Override(0, "override_b")));

            var result = LevelResolver.Resolve(levelRef, patterns, theme);

            Assert.AreEqual(2, result.patterns.Count);
            Assert.AreEqual("override_a", result.patterns[0].obstacles[0].spriteName);
            Assert.AreEqual("override_b", result.patterns[1].obstacles[0].spriteName);
            Assert.AreEqual("default_sprite", result.patterns[0].obstacles[1].spriteName);
            Assert.AreEqual("default_sprite", result.patterns[1].obstacles[1].spriteName);
        }

        #endregion

        #region Metadata Propagation

        [Test]
        public void Resolve_CopiesTexturesAndDecorations()
        {
            var patterns = CreatePatterns(CreateTemplate("p", Slot(0, 9)));
            var theme = CreateTheme();

            var decor = new DecorationPattern
            {
                decorationTiles = new List<DecorationTile>
                {
                    new() { name = "bush", xPos = 5, yPos = 10 }
                }
            };

            var levelRef = new LevelInfoRef
            {
                skyTexture = "sky_tex",
                roadTexture = "road_tex",
                location = "test",
                patternSequence = new List<PatternRef> { Ref("p") },
                decorationPatterns = new List<DecorationPattern> { decor }
            };

            var result = LevelResolver.Resolve(levelRef, patterns, theme);

            Assert.AreEqual("sky_tex", result.skyTexture);
            Assert.AreEqual("road_tex", result.roadTexture);
            Assert.AreEqual(1, result.decorationPatterns.Count);
            Assert.AreEqual("bush", result.decorationPatterns[0].decorationTiles[0].name);
        }

        [Test]
        public void Resolve_PreservesObstaclePositions()
        {
            var patterns = CreatePatterns(
                CreateTemplate("p",
                    new ObstacleSlot { id = 0, type = 0, x = 22.2f, y = -2.8f },
                    new ObstacleSlot { id = 1, type = 0, x = 61.6f, y = -1.8f }));

            var theme = CreateTheme(Mapping(0, "sprite"));
            var levelRef = CreateLevelRef(Ref("p"));

            var result = LevelResolver.Resolve(levelRef, patterns, theme);

            Assert.AreEqual(22.2f, result.patterns[0].obstacles[0].x, 0.001f);
            Assert.AreEqual(-2.8f, result.patterns[0].obstacles[0].y, 0.001f);
            Assert.AreEqual(61.6f, result.patterns[0].obstacles[1].x, 0.001f);
            Assert.AreEqual(-1.8f, result.patterns[0].obstacles[1].y, 0.001f);
        }

        #endregion
    }
}
