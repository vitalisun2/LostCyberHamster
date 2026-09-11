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

        private static HierarchicalLevelCatalog CreateLocationUnlockCatalog()
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

        private static HierarchicalLevelCatalog CreatePartUnlockCatalog()
        {
            return HierarchicalLevelCatalog.Factory.CreateCatalog(new[]
            {
                new HierarchicalLevelCatalog.LocationDefinition(
                    "New York",
                    new[]
                    {
                        new HierarchicalLevelCatalog.PartDefinition(
                            PartOfDayEnum.Morning.ToString(),
                            Enumerable.Range(1, 5)
                                .Select(index => new HierarchicalLevelCatalog.LevelDefinition($"locations/new_york/morning/level_{index:00}"))
                                .ToArray()),
                        new HierarchicalLevelCatalog.PartDefinition(
                            PartOfDayEnum.Afternoon.ToString(),
                            Enumerable.Range(1, 5)
                                .Select(index => new HierarchicalLevelCatalog.LevelDefinition($"locations/new_york/afternoon/level_{index:00}"))
                                .ToArray())
                    })
            });
        }

        private static ProgressService CreateService(HierarchicalLevelCatalog catalog)
        {
            var policy = new DefaultUnlockPolicy(catalog, StarOffset);
            return new ProgressService(catalog, policy);
        }

        private static LevelProgressSnapshot CompleteLevels(
            ProgressService service,
            LevelProgressSnapshot snapshot,
            string locationId,
            string partId,
            params int[] stars)
        {
            foreach (var levelIndex in Enumerable.Range(0, stars.Length))
            {
                snapshot = service.HandleLevelCompleted(
                    snapshot,
                    new LevelProgressKey(locationId, partId, levelIndex),
                    stars[levelIndex]);
            }

            return snapshot;
        }

        [Test]
        public void HandleLevelCompleted_UnlocksNextLevelInsideCurrentPart()
        {
            var catalog = CreatePartUnlockCatalog();
            var service = CreateService(catalog);
            var snapshot = LevelProgressSnapshot.CreateFromCatalog(catalog);

            var locationId = catalog.GetLocationId(0);
            var morningPartId = catalog.GetPartId(0, 0);
            var firstLevelKey = new LevelProgressKey(locationId, morningPartId, 0);
            var secondLevelKey = new LevelProgressKey(locationId, morningPartId, 1);

            var updated = service.HandleLevelCompleted(snapshot, firstLevelKey, stars: 1);

            Assert.AreEqual(1, updated.GetStars(firstLevelKey));
            Assert.IsTrue(updated.TryGet(secondLevelKey, out var unlockedEntry));
            Assert.IsTrue(unlockedEntry.IsUnlocked);
        }

        [Test]
        public void HandleLevelCompleted_DoesNotUnlockNextPartWhenAnyLevelIncomplete()
        {
            var catalog = CreatePartUnlockCatalog();
            var service = CreateService(catalog);
            var snapshot = LevelProgressSnapshot.CreateFromCatalog(catalog);

            var locationId = catalog.GetLocationId(0);
            var morningPartId = catalog.GetPartId(0, 0);
            var afternoonPartId = catalog.GetPartId(0, 1);

            var afterMorning = CompleteLevels(service, snapshot, locationId, morningPartId, 3, 3, 3, 3, 0);

            Assert.IsTrue(afterMorning.TryGet(new LevelProgressKey(locationId, afternoonPartId, 0), out var entry));
            Assert.IsFalse(entry.IsUnlocked);
        }

        [Test]
        public void HandleLevelCompleted_DoesNotUnlockNextPartWhenStarsInsufficient()
        {
            var catalog = CreatePartUnlockCatalog();
            var service = CreateService(catalog);
            var snapshot = LevelProgressSnapshot.CreateFromCatalog(catalog);

            var locationId = catalog.GetLocationId(0);
            var morningPartId = catalog.GetPartId(0, 0);
            var afternoonPartId = catalog.GetPartId(0, 1);

            var afterMorning = CompleteLevels(service, snapshot, locationId, morningPartId, 1, 1, 1, 1, 1);

            Assert.IsTrue(afterMorning.TryGet(new LevelProgressKey(locationId, afternoonPartId, 0), out var entry));
            Assert.IsFalse(entry.IsUnlocked);
        }

        [Test]
        public void HandleLevelCompleted_UnlocksNextPartWhenAllLevelsCompletedAndStarsAtLeastTen()
        {
            var catalog = CreatePartUnlockCatalog();
            var service = CreateService(catalog);
            var snapshot = LevelProgressSnapshot.CreateFromCatalog(catalog);

            var locationId = catalog.GetLocationId(0);
            var morningPartId = catalog.GetPartId(0, 0);
            var afternoonPartId = catalog.GetPartId(0, 1);

            var afterMorning = CompleteLevels(service, snapshot, locationId, morningPartId, 2, 2, 2, 2, 2);

            Assert.IsTrue(afterMorning.TryGet(new LevelProgressKey(locationId, afternoonPartId, 0), out var entry));
            Assert.IsTrue(entry.IsUnlocked);
        }

        [Test]
        public void HandleLevelCompleted_DoesNotRelockAlreadyUnlockedNextPart()
        {
            var catalog = CreatePartUnlockCatalog();
            var service = CreateService(catalog);
            var locationId = catalog.GetLocationId(0);
            var morningPartId = catalog.GetPartId(0, 0);
            var afternoonPartId = catalog.GetPartId(0, 1);
            var afternoonFirstLevelKey = new LevelProgressKey(locationId, afternoonPartId, 0);
            var snapshot = LevelProgressSnapshot.CreateFromCatalog(catalog)
                .Set(new LevelProgressEntry(afternoonFirstLevelKey, true, 0));

            var afterMorning = CompleteLevels(service, snapshot, locationId, morningPartId, 1, 1, 1, 1, 1);

            Assert.IsTrue(afterMorning.TryGet(afternoonFirstLevelKey, out var entry));
            Assert.IsTrue(entry.IsUnlocked);
        }

        [Test]
        public void GetStarsToOpenNextLocation_ReturnsMissingStars()
        {
            var catalog = CreateLocationUnlockCatalog();
            var service = CreateService(catalog);
            var snapshot = LevelProgressSnapshot.CreateFromCatalog(catalog);

            var locationId = catalog.GetLocationId(0);
            var morningPartId = catalog.GetPartId(0, 0);

            var afterMorning = service.HandleLevelCompleted(snapshot, new LevelProgressKey(locationId, morningPartId, 0), stars: 3);

            Assert.AreEqual(1, service.GetStarsToOpenNextLocation(afterMorning));
        }
    }
}
