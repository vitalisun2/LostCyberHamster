using Assets.Scripts.Common.Models;
using GameManagement;
using NUnit.Framework;
using UnityEngine;

namespace PlayerDataTests
{
    public class PlayerDataMigrationTests
    {
        [Test]
        public void FromJson_OldSchema_MigratesToLevelKey()
        {
            const string legacyJson = "{\"CurrentLevel\":\"level_07\"," +
                                       "\"LevelStars\":[0,1,3,2,0,0,3]," +
                                       "\"OpenedLevels\":{}," +
                                       "\"DataVersion\":1}";

            var data = JsonUtility.FromJson<PlayerData>(legacyJson);

            Assert.That(data.CurrentLevelKey.LocationId, Is.EqualTo("paris"));
            Assert.That(data.CurrentLevelKey.Part, Is.EqualTo(PartOfDay.Evening));
            Assert.That(data.CurrentLevelKey.Index, Is.EqualTo(1));
            Assert.That(data.StarsByLevel.Count, Is.EqualTo(3));
        }
    }
}
