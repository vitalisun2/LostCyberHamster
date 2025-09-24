using Assets.Scripts.Common.Models;
using Assets.Scripts.System;
using GameManagement;
using NUnit.Framework;

namespace LevelManagerTests
{
    public class SetCurrentKeyTests
    {
        [SetUp]
        public void Setup()
        {
            GameDataManager.PlayerData = new PlayerData();
        }

        [Test]
        public void SetCurrentKey_UpdatesPlayerData()
        {
            var key = LevelKey.Parse("paris/afternoon/2");

            LevelManager.SetCurrentKey(key);

            Assert.That(GameDataManager.PlayerData.CurrentLevelKey, Is.EqualTo(key));
        }
    }
}
