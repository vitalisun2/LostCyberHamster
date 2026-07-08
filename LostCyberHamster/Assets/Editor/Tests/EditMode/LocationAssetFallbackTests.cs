using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Common.Models;
using Assets.Scripts.System;
using NUnit.Framework;

namespace Assets.Tests.EditMode
{
    public class LocationAssetFallbackTests
    {
        [Test]
        public void TryBuildFallbackLabel_ReplacesLocationPrefix()
        {
            var result = LocationAssetFallback.TryBuildFallbackLabel("Paris decor sprites", "Paris", "New York");

            Assert.AreEqual("New York decor sprites", result);
        }

        [Test]
        public void TryBuildFallbackLabel_ReturnsNull_WhenPrefixMissing()
        {
            var result = LocationAssetFallback.TryBuildFallbackLabel("decor sprites", "Paris", "New York");

            Assert.IsNull(result);
        }

        [Test]
        public void ToSlug_ReplacesWhitespacesAndHyphens()
        {
            Assert.AreEqual("new_york", LocationAssetFallback.ToSlug("New York"));
            Assert.AreEqual("los_angeles", LocationAssetFallback.ToSlug("Los-Angeles"));
        }

        [Test]
        public void TryBuildFallbackBackgroundKey_UsesOriginalSuffix()
        {
            var result = LocationAssetFallback.TryBuildFallbackBackgroundKey("bg_paris_evening", "New York", "Evening");

            Assert.AreEqual("bg_new_york_evening", result);
        }

        [Test]
        public void TryBuildFallbackBackgroundKey_UsesPartOfDayFallback()
        {
            var result = LocationAssetFallback.TryBuildFallbackBackgroundKey(null, "New York", "Morning");

            Assert.AreEqual("bg_new_york_morning", result);
        }

        [Test]
        public void MergeLocationTheme_FillsMissingTypesFromFallback()
        {
            var primary = new LocationTheme
            {
                obstacle_sprite_to_type_mappings = new List<SpriteTypeMapping>
                {
                    new() { type = 9, sprites = new List<string> { "coin" } }
                }
            };
            var fallback = new LocationTheme
            {
                obstacle_sprite_to_type_mappings = new List<SpriteTypeMapping>
                {
                    new() { type = 1, sprites = new List<string> { "obstacle_new_york_big_alive_1_idle" } },
                    new() { type = 9, sprites = new List<string> { "fallback_coin" } }
                }
            };

            var result = LocationAssetFallback.MergeLocationTheme(primary, fallback);

            Assert.AreEqual("coin", result.obstacle_sprite_to_type_mappings.Single(m => m.type == 9).sprites[0]);
            Assert.AreEqual(
                "obstacle_new_york_big_alive_1_idle",
                result.obstacle_sprite_to_type_mappings.Single(m => m.type == 1).sprites[0]);
        }
    }
}
