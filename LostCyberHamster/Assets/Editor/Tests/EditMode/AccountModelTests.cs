using LostCyberHamster.Account;
using NUnit.Framework;

namespace Assets.Tests.EditMode
{
    /// <summary>
    /// Проверяет неизменяемые модели account-слоя, их значения по умолчанию и фабрики результатов.
    /// </summary>
    public class AccountModelTests
    {
        [Test]
        public void GivenDefaultSnapshot_WhenPropertiesAreRead_ThenSafeUnknownValuesAreReturned()
        {
            // Arrange
            AccountSnapshot snapshot = default;

            // Act / Assert
            Assert.AreEqual(AccountState.Unknown, snapshot.State);
            Assert.AreEqual(string.Empty, snapshot.PlayerId);
            Assert.IsFalse(snapshot.IsSignedIn);
            Assert.IsFalse(snapshot.IsLinked);
            Assert.AreEqual(string.Empty, snapshot.ErrorMessage);
            Assert.IsFalse(snapshot.CanUseCloudSave);
        }

        [Test]
        public void GivenNullSnapshotStrings_WhenSnapshotIsCreated_ThenStringsAreNormalizedToEmpty()
        {
            // Arrange / Act
            var snapshot = new AccountSnapshot(AccountState.Error, null, true, false, null);

            // Assert
            Assert.AreEqual(string.Empty, snapshot.PlayerId);
            Assert.AreEqual(string.Empty, snapshot.ErrorMessage);
        }

        [Test]
        public void GivenUnknownSnapshotFactory_WhenSnapshotIsCreated_ThenExpectedDefaultsAreReturned()
        {
            // Arrange / Act
            AccountSnapshot snapshot = AccountSnapshot.Unknown;

            // Assert
            Assert.AreEqual(AccountState.Unknown, snapshot.State);
            Assert.AreEqual(string.Empty, snapshot.PlayerId);
            Assert.IsFalse(snapshot.IsSignedIn);
            Assert.IsFalse(snapshot.IsLinked);
            Assert.AreEqual(string.Empty, snapshot.ErrorMessage);
            Assert.IsFalse(snapshot.CanUseCloudSave);
        }

        [TestCase(AccountState.Unknown, false, false)]
        [TestCase(AccountState.Unknown, true, false)]
        [TestCase(AccountState.Guest, false, false)]
        [TestCase(AccountState.Guest, true, true)]
        [TestCase(AccountState.Linked, false, false)]
        [TestCase(AccountState.Linked, true, true)]
        [TestCase(AccountState.Offline, false, false)]
        [TestCase(AccountState.Offline, true, false)]
        [TestCase(AccountState.Error, false, false)]
        [TestCase(AccountState.Error, true, false)]
        public void GivenAccountStateAndSignInFlag_WhenCloudSaveAvailabilityIsRead_ThenExpectedValueIsReturned(
            AccountState state,
            bool isSignedIn,
            bool expected)
        {
            // Arrange
            var snapshot = new AccountSnapshot(state, "player", isSignedIn, state == AccountState.Linked, string.Empty);

            // Act
            bool actual = snapshot.CanUseCloudSave;

            // Assert
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void GivenAccountLinkStatus_WhenDefaultValueIsRead_ThenItIsUnknownZero()
        {
            // Arrange / Act
            AccountLinkStatus status = default;

            // Assert
            Assert.AreEqual(0, (int)AccountLinkStatus.Unknown);
            Assert.AreEqual(AccountLinkStatus.Unknown, status);
        }

        [Test]
        public void GivenDefaultLinkResult_WhenPropertiesAreRead_ThenSafeUnknownValuesAreReturned()
        {
            // Arrange
            AccountLinkResult result = default;

            // Act / Assert
            Assert.AreEqual(AccountLinkStatus.Unknown, result.Status);
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(string.Empty, result.PlayerId);
            Assert.AreEqual(string.Empty, result.ErrorMessage);
        }

        [TestCase(null, "")]
        [TestCase("", "")]
        [TestCase("not started", "not started")]
        public void GivenUnknownFactory_WhenResultIsCreated_ThenUnknownResultIsNormalized(
            string errorMessage,
            string expectedErrorMessage)
        {
            // Arrange / Act
            AccountLinkResult result = AccountLinkResult.Unknown(errorMessage);

            // Assert
            Assert.AreEqual(AccountLinkStatus.Unknown, result.Status);
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(string.Empty, result.PlayerId);
            Assert.AreEqual(expectedErrorMessage, result.ErrorMessage);
        }

        [TestCase("player", "player")]
        public void GivenSuccessFactory_WhenResultIsCreated_ThenSuccessResultIsNormalized(
            string playerId,
            string expectedPlayerId)
        {
            // Arrange / Act
            AccountLinkResult result = AccountLinkResult.Success(playerId);

            // Assert
            Assert.AreEqual(AccountLinkStatus.Success, result.Status);
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(expectedPlayerId, result.PlayerId);
            Assert.AreEqual(string.Empty, result.ErrorMessage);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void GivenMissingPlayerId_WhenSuccessFactoryIsCalled_ThenFailedResultIsReturned(string playerId)
        {
            AccountLinkResult result = AccountLinkResult.Success(playerId);

            Assert.AreEqual(AccountLinkStatus.Failed, result.Status);
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(string.Empty, result.PlayerId);
            Assert.AreEqual("UGS player ID is empty.", result.ErrorMessage);
        }

        [TestCase(null, "")]
        [TestCase("conflict", "conflict")]
        public void GivenAlreadyLinkedFactory_WhenResultIsCreated_ThenConflictResultIsNormalized(
            string errorMessage,
            string expectedErrorMessage)
        {
            // Arrange / Act
            AccountLinkResult result = AccountLinkResult.AlreadyLinked(errorMessage);

            // Assert
            Assert.AreEqual(AccountLinkStatus.AlreadyLinked, result.Status);
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(string.Empty, result.PlayerId);
            Assert.AreEqual(expectedErrorMessage, result.ErrorMessage);
        }

        [TestCase(null, "")]
        [TestCase("failed", "failed")]
        public void GivenFailedFactory_WhenResultIsCreated_ThenFailedResultIsNormalized(
            string errorMessage,
            string expectedErrorMessage)
        {
            // Arrange / Act
            AccountLinkResult result = AccountLinkResult.Failed(errorMessage);

            // Assert
            Assert.AreEqual(AccountLinkStatus.Failed, result.Status);
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(string.Empty, result.PlayerId);
            Assert.AreEqual(expectedErrorMessage, result.ErrorMessage);
        }
    }
}
