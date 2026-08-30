using System.Collections.Generic;
using Assets.Scripts.System;
using GameManagement;
using NUnit.Framework;
using UnityEngine;
using Vues.GameCore;
using Vues.GameCore.Quests;

namespace Assets.Tests.EditMode
{
    public sealed class PlayerDataValidatorTests
    {
        private HierarchicalLevelCatalog _previousCatalog;
        private List<Skin> _previousSkins;

        [SetUp]
        public void SetUp()
        {
            _previousCatalog = LevelCatalogService.Catalog;
            LevelCatalogService.Reset();

            _previousSkins = new List<Skin>(SkinManager.AvailableSkins);
            SkinManager.AvailableSkins.Clear();
            foreach (int skinId in new[] { 0, 4, 5, 6 })
            {
                SkinManager.AvailableSkins.Add(new Skin { Id = skinId });
            }
        }

        [TearDown]
        public void TearDown()
        {
            LevelCatalogService.Configure(_previousCatalog);
            SkinManager.AvailableSkins.Clear();
            SkinManager.AvailableSkins.AddRange(_previousSkins);
        }

        [Test]
        public void Validate_ValidData_DoesNotChangeData()
        {
            var data = CreateValidData();
            string jsonBeforeValidation = data.ToJson();

            var result = PlayerDataValidator.Validate(data);

            Assert.AreEqual(PlayerDataValidationStatus.Valid, result.Status);
            Assert.AreEqual(jsonBeforeValidation, data.ToJson());
        }

        [Test]
        public void RepairSafe_NullCollectionsAndExactDuplicates_RevalidatesAndIsIdempotent()
        {
            var data = CreateValidData();
            data.QuestStates = null;
            data.PurchasedSkinIds = new List<int> { 0, 0 };

            var initialResult = PlayerDataValidator.Validate(data);
            Assert.AreEqual(PlayerDataValidationStatus.Repairable, initialResult.Status);

            PlayerDataValidator.RepairSafe(data, initialResult);

            var repairedResult = PlayerDataValidator.Validate(data);
            Assert.AreEqual(PlayerDataValidationStatus.Valid, repairedResult.Status);
            CollectionAssert.AreEqual(new[] { 0 }, data.PurchasedSkinIds);
            Assert.IsNotNull(data.QuestStates);

            string jsonAfterFirstRepair = data.ToJson();
            PlayerDataValidator.RepairSafe(data, repairedResult);

            Assert.AreEqual(jsonAfterFirstRepair, data.ToJson());
            Assert.AreEqual(PlayerDataValidationStatus.Valid, PlayerDataValidator.Validate(data).Status);
        }

        [Test]
        public void Validate_NullPlayerData_IsRejected()
        {
            var result = PlayerDataValidator.Validate(null);

            Assert.AreEqual(PlayerDataValidationStatus.Rejected, result.Status);
            Assert.AreEqual("player_data_missing", result.Reason);
        }

        [Test]
        public void Validate_NegativeResourceBalance_IsRejected()
        {
            var data = CreateValidData();
            data.Money = -1;

            var result = PlayerDataValidator.Validate(data);

            Assert.AreEqual(PlayerDataValidationStatus.Rejected, result.Status);
            Assert.AreEqual("negative_resource_balance", result.Reason);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void RepairSafe_RetiredSkins_RemovesThemAndResetsAppliedSkin(
            int appliedRetiredSkinId)
        {
            var data = CreateValidData();
            data.DevelopmentProgressVersion = 1;
            data.PlayerLevel = 10;
            data.DevelopmentPoints = 3;
            data.PurchasedSkinIds = new List<int> { 0, 1, 2, 3 };
            data.UnlockedSkinIds = new List<int> { 0, 1, 2, 3 };
            data.AppliedSkinId = appliedRetiredSkinId;

            var initialResult = PlayerDataValidator.Validate(data);
            Assert.AreEqual(
                PlayerDataValidationStatus.Repairable,
                initialResult.Status);

            PlayerDataValidator.RepairSafe(data, initialResult);

            CollectionAssert.AreEqual(new[] { 0 }, data.PurchasedSkinIds);
            CollectionAssert.AreEqual(new[] { 0 }, data.UnlockedSkinIds);
            Assert.AreEqual(0, data.AppliedSkinId);
            Assert.AreEqual(3, data.DevelopmentPoints);
            Assert.AreEqual(
                CharacterDevelopmentService.CurrentProgressVersion,
                data.DevelopmentProgressVersion);
            Assert.AreEqual(
                PlayerDataValidationStatus.Valid,
                PlayerDataValidator.Validate(data).Status);

            string jsonAfterRepair = data.ToJson();
            PlayerDataValidator.RepairSafe(
                data,
                PlayerDataValidator.Validate(data));
            Assert.AreEqual(jsonAfterRepair, data.ToJson());
        }

        [Test]
        public void Validate_UnknownNonRetiredSkin_IsRejected()
        {
            var data = CreateValidData();
            data.PurchasedSkinIds.Add(999);

            var result = PlayerDataValidator.Validate(data);

            Assert.AreEqual(PlayerDataValidationStatus.Rejected, result.Status);
            Assert.AreEqual("unknown_purchased_skin", result.Reason);
        }

        [Test]
        public void Validate_RewardWithoutCompletion_IsRejected()
        {
            var data = CreateValidData();
            data.QuestStates.Add(new Quest
            {
                QuestId = "quest-002",
                IsCompleted = false,
                IsRewardClaimed = true
            });

            var result = PlayerDataValidator.Validate(data);

            Assert.AreEqual(PlayerDataValidationStatus.Rejected, result.Status);
            Assert.AreEqual("invalid_quest_state", result.Reason);
        }

        [TestCase(
            "{\"LocationId\":\" \" ,\"PartOfDayId\":\"day\",\"LevelIndex\":0,\"IsUnlocked\":true,\"Stars\":1}",
            "invalid_level_progress")]
        [TestCase(
            "{\"LocationId\":\"location\",\"PartOfDayId\":\"day\",\"LevelIndex\":-1,\"IsUnlocked\":true,\"Stars\":1}",
            "invalid_level_progress")]
        [TestCase(
            "{\"LocationId\":\"location\",\"PartOfDayId\":\"day\",\"LevelIndex\":0,\"IsUnlocked\":true,\"Stars\":-1}",
            "invalid_level_progress")]
        [TestCase(
            "{\"LocationId\":\"location\",\"PartOfDayId\":\"day\",\"LevelIndex\":0,\"IsUnlocked\":true,\"Stars\":4}",
            "invalid_level_progress")]
        [TestCase(
            "{\"LocationId\":\"location\",\"PartOfDayId\":\"day\",\"LevelIndex\":0,\"IsUnlocked\":false,\"Stars\":0}," +
            "{\"LocationId\":\"location\",\"PartOfDayId\":\"day\",\"LevelIndex\":0,\"IsUnlocked\":true,\"Stars\":1}",
            "conflicting_level_progress")]
        public void Validate_RawInvalidLevelProgress_IsRejected(string entriesJson, string expectedReason)
        {
            var data = FromRawProgressJson(entriesJson);

            var result = PlayerDataValidator.Validate(data);

            Assert.AreEqual(PlayerDataValidationStatus.Rejected, result.Status);
            Assert.AreEqual(expectedReason, result.Reason);
        }

        [Test]
        public void RepairSafe_ExactRawLevelProgressDuplicate_RevalidatesAndIsIdempotent()
        {
            const string entry =
                "{\"LocationId\":\"location\",\"PartOfDayId\":\"day\",\"LevelIndex\":0," +
                "\"IsUnlocked\":true,\"Stars\":2}";
            var data = FromRawProgressJson(entry + "," + entry);

            var initialResult = PlayerDataValidator.Validate(data);
            Assert.AreEqual(PlayerDataValidationStatus.Repairable, initialResult.Status);

            PlayerDataValidator.RepairSafe(data, initialResult);

            var repairedResult = PlayerDataValidator.Validate(data);
            Assert.AreEqual(PlayerDataValidationStatus.Valid, repairedResult.Status);
            Assert.AreEqual(1, data.Progress.Entries.Count);

            string jsonAfterFirstRepair = data.ToJson();
            PlayerDataValidator.RepairSafe(data, repairedResult);

            Assert.AreEqual(jsonAfterFirstRepair, data.ToJson());
            Assert.AreEqual(PlayerDataValidationStatus.Valid, PlayerDataValidator.Validate(data).Status);
        }

        private static PlayerData CreateValidData()
        {
            return new PlayerData
            {
                PurchasedSkinIds = new List<int> { 0 },
                AppliedSkinId = 0,
                QuestStates = new List<Quest>()
            };
        }

        private static PlayerData FromRawProgressJson(string entriesJson)
        {
            string json =
                "{\"PurchasedSkinIds\":[0],\"AppliedSkinId\":0,\"QuestStates\":[]," +
                "\"_serializedProgress\":[" + entriesJson + "]}";
            return JsonUtility.FromJson<PlayerData>(json);
        }
    }
}
