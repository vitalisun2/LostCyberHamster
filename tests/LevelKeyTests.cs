using Assets.Scripts.Common.Models;
using NUnit.Framework;

namespace LevelKeyTests
{
    public class ParseRoundTripTests
    {
        [TestCase("paris/night/3", "paris", PartOfDay.Night, 3)]
        [TestCase("tokyo/morning/1", "tokyo", PartOfDay.Morning, 1)]
        [TestCase("new_york/evening/12", "new_york", PartOfDay.Evening, 12)]
        public void Parse_ToCompactString_RoundTrip(string compact, string expectedLocation, PartOfDay expectedPart, int expectedIndex)
        {
            var key = LevelKey.Parse(compact);

            Assert.That(key.LocationId, Is.EqualTo(expectedLocation));
            Assert.That(key.Part, Is.EqualTo(expectedPart));
            Assert.That(key.Index, Is.EqualTo(expectedIndex));

            var roundTrip = LevelKey.Parse(key.ToCompactString());
            Assert.That(roundTrip, Is.EqualTo(key));
        }

        [Test]
        public void TryParse_InvalidFormat_ReturnsFalse()
        {
            var success = LevelKey.TryParse("invalid", out _);
            Assert.False(success);
        }
    }
}
