using LostCyberHamster.Account;
using NUnit.Framework;

namespace Assets.Tests.EditMode
{
    public class AccountModelTests
    {
        [Test]
        public void AccountSnapshot_Unknown_HasExpectedDefaults()
        {
            var snapshot = AccountSnapshot.Unknown;

            Assert.AreEqual(AccountState.Unknown, snapshot.State);
            Assert.AreEqual(string.Empty, snapshot.PlayerId);
            Assert.IsFalse(snapshot.IsSignedIn);
            Assert.IsFalse(snapshot.IsLinked);
            Assert.AreEqual(string.Empty, snapshot.ErrorMessage);
            Assert.IsFalse(snapshot.CanUseCloudSave);
        }

        [TestCase(AccountState.Guest, true, true)]
        [TestCase(AccountState.Linked, true, true)]
        [TestCase(AccountState.Offline, true, false)]
        [TestCase(AccountState.Error, true, false)]
        [TestCase(AccountState.Guest, false, false)]
        public void AccountSnapshot_CanUseCloudSave_WhenSignedInAndNotOfflineOrError(
            AccountState state,
            bool isSignedIn,
            bool expected)
        {
            var snapshot = new AccountSnapshot(state, "player", isSignedIn, state == AccountState.Linked, string.Empty);

            Assert.AreEqual(expected, snapshot.CanUseCloudSave);
        }

        [Test]
        public void AccountLinkResult_Success_HasSuccessStatusAndPlayerId()
        {
            var result = AccountLinkResult.Success("player");

            Assert.AreEqual(AccountLinkStatus.Success, result.Status);
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("player", result.PlayerId);
            Assert.AreEqual(string.Empty, result.ErrorMessage);
        }

        [Test]
        public void AccountLinkResult_AlreadyLinked_HasAlreadyLinkedStatusAndError()
        {
            var result = AccountLinkResult.AlreadyLinked("conflict");

            Assert.AreEqual(AccountLinkStatus.AlreadyLinked, result.Status);
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(string.Empty, result.PlayerId);
            Assert.AreEqual("conflict", result.ErrorMessage);
        }

        [Test]
        public void AccountLinkResult_Failed_HasFailedStatusAndError()
        {
            var result = AccountLinkResult.Failed("failed");

            Assert.AreEqual(AccountLinkStatus.Failed, result.Status);
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(string.Empty, result.PlayerId);
            Assert.AreEqual("failed", result.ErrorMessage);
        }
    }
}
