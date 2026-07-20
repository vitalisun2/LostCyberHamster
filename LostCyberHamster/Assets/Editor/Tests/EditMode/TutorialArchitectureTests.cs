using System;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.Tutorial;
using NUnit.Framework;

namespace Assets.Tests.EditMode
{
    public sealed class TutorialArchitectureTests
    {
        [Test]
        public void DecideRouting_FirstIncompleteLevel_ReturnsTutorialTarget()
        {
            TutorialRoutingDecision decision = TutorialLaunchService.DecideRouting(
                TutorialConstants.FirstGameplayLevelAddress,
                isTutorialCompleted: false);

            Assert.IsTrue(decision.ShouldRedirect);
            Assert.AreEqual(TutorialConstants.CoreLessonLevelAddress, decision.TargetLevelAddress);
        }

        [TestCase(TutorialConstants.FirstGameplayLevelAddress, true)]
        [TestCase(TutorialConstants.CoreLessonLevelAddress, false)]
        [TestCase("02_Paris/Morning/level_02", false)]
        public void DecideRouting_NonEligibleState_ReturnsNoRedirect(string levelAddress, bool completed)
        {
            TutorialRoutingDecision decision = TutorialLaunchService.DecideRouting(levelAddress, completed);

            Assert.IsFalse(decision.ShouldRedirect);
            Assert.IsEmpty(decision.TargetLevelAddress);
        }

        [Test]
        public void TransitionGuard_AllowsOnlyOneTransitionUntilReset()
        {
            var guard = new TutorialTransitionGuard();

            Assert.IsTrue(guard.TryBegin());
            Assert.IsFalse(guard.TryBegin());

            guard.Reset();

            Assert.IsTrue(guard.TryBegin());
        }

        [Test]
        public void GameplayInputGate_RemainsBlockedUntilEveryOwnerReleases()
        {
            var firstOwner = new object();
            var secondOwner = new object();

            try
            {
                GameplayInputGate.SetBlocked(firstOwner, true);
                GameplayInputGate.SetBlocked(secondOwner, true);
                GameplayInputGate.SetBlocked(firstOwner, false);

                Assert.IsTrue(GameplayInputGate.IsBlocked);

                GameplayInputGate.SetBlocked(secondOwner, false);

                Assert.IsFalse(GameplayInputGate.IsBlocked);
            }
            finally
            {
                GameplayInputGate.SetBlocked(firstOwner, false);
                GameplayInputGate.SetBlocked(secondOwner, false);
            }
        }

        [Test]
        public void GameplayInputGate_NullOwner_IsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => GameplayInputGate.SetBlocked(null, true));
        }
    }
}
