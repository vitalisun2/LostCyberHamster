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
    }
}
