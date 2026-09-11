using System;
using System.Collections.Generic;
using NUnit.Framework;
using Vues.GameCore.Quests;

namespace Assets.Tests.EditMode
{
    public sealed class DailyQuestSchedulerTests
    {
        private readonly DailyQuestScheduler _scheduler = new();

        [Test]
        public void ShouldGenerate_WhenGenerationDateIsStaleAndCurrentDateIsAlreadyInHistory_ReturnsTrue()
        {
            var state = new DailyQuestSetState
            {
                GenerationDate = "2026-09-10",
                UsedGenerationDates = new List<string>
                {
                    "2026-09-10",
                    "2026-09-11"
                }
            };

            bool result = _scheduler.ShouldGenerate(
                state,
                new DateTime(2026, 9, 11, 18, 0, 0));

            Assert.IsTrue(result);
        }

        [Test]
        public void ShouldGenerate_WhenGenerationDateMatchesCurrentDate_ReturnsFalse()
        {
            var state = new DailyQuestSetState
            {
                GenerationDate = "2026-09-11"
            };

            bool result = _scheduler.ShouldGenerate(
                state,
                new DateTime(2026, 9, 11, 23, 59, 59));

            Assert.IsFalse(result);
        }

        [Test]
        public void ShouldGenerate_WhenGenerationDateIsInFuture_ReturnsFalse()
        {
            var state = new DailyQuestSetState
            {
                GenerationDate = "2026-09-12",
                UsedGenerationDates = new List<string>
                {
                    "2026-09-11",
                    "2026-09-12"
                }
            };

            bool result = _scheduler.ShouldGenerate(
                state,
                new DateTime(2026, 9, 11, 18, 0, 0));

            Assert.IsFalse(result);
        }
    }
}