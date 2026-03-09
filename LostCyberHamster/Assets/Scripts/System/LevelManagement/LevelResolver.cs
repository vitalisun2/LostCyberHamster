using System;
using System.Collections.Generic;
using Assets.Scripts.Common.Models;
using UnityEngine;

namespace Assets.Scripts.System.LevelManagement
{
    /// <summary>
    /// Resolves reference-based level format (LevelInfoRef) into concrete LevelInfo
    /// by combining pattern templates, location theme, overrides, and seed-based randomness.
    /// </summary>
    public static class LevelResolver
    {
        /// <summary>
        /// Resolves a reference-based level into a concrete LevelInfo with filled spriteName for each obstacle.
        /// Resolution priority: manual override > seed-based random > theme default > universal name.
        /// </summary>
        public static LevelInfo Resolve(
            LevelInfoRef levelRef,
            PatternsCollection patternsCollection,
            LocationTheme theme)
        {
            var result = new LevelInfo
            {
                skyTexture = levelRef.skyTexture,
                background2Texture = levelRef.background2Texture,
                backgroundTexture = levelRef.backgroundTexture,
                roadTexture = levelRef.roadTexture,
                decorationPatterns = levelRef.decorationPatterns ?? new List<DecorationPattern>()
            };

            var themeLookup = BuildThemeLookup(theme);
            var patternLookup = BuildPatternLookup(patternsCollection);

            result.patterns = new List<Pattern>();

            for (int i = 0; i < levelRef.patternSequence.Count; i++)
            {
                var patternRef = levelRef.patternSequence[i];
                var refName = patternRef.@ref;

                if (!patternLookup.TryGetValue(refName, out var template))
                {
                    Debug.LogError($"[LevelResolver] Pattern '{refName}' not found in PatternsCollection. Skipping.");
                    continue;
                }

                var overrideLookup = BuildOverrideLookup(patternRef.overrides);

                var resolvedPattern = new Pattern
                {
                    name = template.name,
                    desсription = template.description,
                    obstacles = new List<ObstacleModel>()
                };

                foreach (var slot in template.obstacles)
                {
                    var resolved = new ObstacleModel
                    {
                        type = slot.type,
                        x = slot.x,
                        y = slot.y
                    };

                    resolved.spriteName = ResolveSpriteName(
                        slot, patternRef.spriteSeed, overrideLookup, themeLookup);

                    resolvedPattern.obstacles.Add(resolved);
                }

                result.patterns.Add(resolvedPattern);
            }

            return result;
        }

        private static string ResolveSpriteName(
            ObstacleSlot slot,
            int spriteSeed,
            Dictionary<int, SpriteOverride> overrideLookup,
            Dictionary<int, SpriteTypeMapping> themeLookup)
        {
            // Priority 1: manual override
            if (overrideLookup.TryGetValue(slot.id, out var spriteOverride))
            {
                return spriteOverride.spriteName;
            }

            // Priority 2/3: theme (with optional seed)
            if (themeLookup.TryGetValue(slot.type, out var mapping))
            {
                if (spriteSeed != 0 && mapping.sprites.Count > 1)
                {
                    var rng = new global::System.Random(spriteSeed + slot.id);
                    var index = rng.Next(mapping.sprites.Count);
                    return mapping.sprites[index];
                }

                return mapping.@default ?? (mapping.sprites.Count > 0 ? mapping.sprites[0] : null);
            }

            // Priority 4: universal name for collectables
            return GetUniversalSpriteName(slot.type);
        }

        private static string GetUniversalSpriteName(int type)
        {
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

        private static Dictionary<int, SpriteTypeMapping> BuildThemeLookup(LocationTheme theme)
        {
            var lookup = new Dictionary<int, SpriteTypeMapping>();
            if (theme?.obstacle_sprite_to_type_mappings == null) return lookup;

            foreach (var mapping in theme.obstacle_sprite_to_type_mappings)
            {
                lookup[mapping.type] = mapping;
            }

            return lookup;
        }

        private static Dictionary<string, PatternTemplate> BuildPatternLookup(PatternsCollection collection)
        {
            var lookup = new Dictionary<string, PatternTemplate>(StringComparer.OrdinalIgnoreCase);
            if (collection?.patterns == null) return lookup;

            foreach (var template in collection.patterns)
            {
                lookup[template.name] = template;
            }

            return lookup;
        }

        private static Dictionary<int, SpriteOverride> BuildOverrideLookup(List<SpriteOverride> overrides)
        {
            var lookup = new Dictionary<int, SpriteOverride>();
            if (overrides == null) return lookup;

            foreach (var ov in overrides)
            {
                lookup[ov.obstacleId] = ov;
            }

            return lookup;
        }
    }
}
