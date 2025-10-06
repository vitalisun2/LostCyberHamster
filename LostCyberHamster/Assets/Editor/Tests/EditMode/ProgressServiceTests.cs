using System.Linq;
using Assets.Scripts.Common.Models;
using Assets.Scripts.System;
using GameManagement.Progress;
using NUnit.Framework;

namespace Assets.Tests.EditMode
{
    public class ProgressServiceTests
    {
        private const int StarOffset = 2;

        private static HierarchicalLevelCatalog CreateCatalog()
        {
            return HierarchicalLevelCatalog.Factory.CreateCatalog(new[]
            {
                new HierarchicalLevelCatalog.LocationDefinition(
                    "New York",
                    new[]
                    {
                        new HierarchicalLevelCatalog.PartDefinition(
                            PartOfDayEnum.Morning.ToString(),
                            new[]
                            {
                                new HierarchicalLevelCatalog.LevelDefinition("locations/new_york/morning/level_01")
                            }),
                        new HierarchicalLevelCatalog.PartDefinition(
                            PartOfDayEnum.Afternoon.ToString(),
                            new[]
                            {
                                new HierarchicalLevelCatalog.LevelDefinition("locations/new_york/afternoon/level_01")
                            })
                    }),
                new HierarchicalLevelCatalog.LocationDefinition(
                    "Paris",
                    new[]
                    {
                        new HierarchicalLevelCatalog.PartDefinition(
                            PartOfDayEnum.Morning.ToString(),
                            new[]
                            {
                                new HierarchicalLevelCatalog.LevelDefinition("locations/paris/morning/level_01")
                            })
                    })
            });
        }

        private static ProgressService CreateService(HierarchicalLevelCatalog catalog)
        {
            var policy = new DefaultUnlockPolicy(catalog, StarOffset);
            return new ProgressService(catalog, policy);
        }

        [Test]
        public void HandleLevelCompleted_UnlocksNextLevelInSameLocation()
        {
            var catalog = CreateCatalog();
            var service = CreateService(catalog);
            var snapshot = LevelProgressSnapshot.CreateFromCatalog(catalog);

            var locationId = catalog.GetLocationId(0);
            var morningPartId = catalog.GetPartId(0, 0);
            var afternoonPartId = catalog.GetPartId(0, 1);
            var firstLevelKey = new LevelProgressKey(locationId, morningPartId, 0);

            var updated = service.HandleLevelCompleted(snapshot, firstLevelKey, stars: 1);

            Assert.AreEqual(1, updated.GetStars(firstLevelKey));
            Assert.IsTrue(updated.TryGet(new LevelProgressKey(locationId, afternoonPartId, 0), out var unlockedEntry));
            Assert.IsTrue(unlockedEntry.IsUnlocked);
        }

        [Test]
        public void HandleLevelCompleted_DoesNotUnlockNextLocationWhenStarsInsufficient()
        {
            var catalog = CreateCatalog();
            var service = CreateService(catalog);
            var snapshot = LevelProgressSnapshot.CreateFromCatalog(catalog);

            var locationId = catalog.GetLocationId(0);
            var morningPartId = catalog.GetPartId(0, 0);
            var afternoonPartId = catalog.GetPartId(0, 1);
            var parisLocationId = catalog.GetLocationId(1);
            var parisMorningId = catalog.GetPartId(1, 0);

            var afterMorning = service.HandleLevelCompleted(snapshot, new LevelProgressKey(locationId, morningPartId, 0), stars: 0);
            var afterAfternoon = service.HandleLevelCompleted(afterMorning, new LevelProgressKey(locationId, afternoonPartId, 0), stars: 0);

            Assert.IsTrue(!afterAfternoon.TryGet(new LevelProgressKey(parisLocationId, parisMorningId, 0), out var entry) || !entry.IsUnlocked);
        }

        [Test]
        public void GetStarsToOpenNextLocation_ReturnsMissingStars()
        {
            var catalog = CreateCatalog();
            var service = CreateService(catalog);
            var snapshot = LevelProgressSnapshot.CreateFromCatalog(catalog);

            var locationId = catalog.GetLocationId(0);
            var morningPartId = catalog.GetPartId(0, 0);

            var afterMorning = service.HandleLevelCompleted(snapshot, new LevelProgressKey(locationId, morningPartId, 0), stars: 3);

            Assert.AreEqual(1, service.GetStarsToOpenNextLocation(afterMorning));
        }
    }
}
