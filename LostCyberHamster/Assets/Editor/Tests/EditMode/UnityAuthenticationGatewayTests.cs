using System;
using System.Threading.Tasks;
using LostCyberHamster.Account;
using NUnit.Framework;
using Unity.Services.Authentication;
using Unity.Services.Core;

namespace Assets.Tests.EditMode
{
    /// <summary>
    /// Проверяет преобразование и делегирование Unity Authentication gateway через детерминированный SDK-порт.
    /// </summary>
    [Timeout(5000)]
    [Category("AccountDevTools")]
    public sealed class UnityAuthenticationGatewayTests
    {
        [Test]
        public void Constructor_GivenNullSdk_WhenCreated_ThenThrows()
        {
            // Arrange / Act / Assert
            Assert.Throws<ArgumentNullException>(() => new UnityAuthenticationGateway(null));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void IsSignedIn_GivenSdkValue_WhenRead_ThenReturnsSdkValue(bool expected)
        {
            // Arrange
            var sdk = new FakeUnityAuthenticationSdk { IsSignedIn = expected };
            var gateway = new UnityAuthenticationGateway(sdk);

            // Act
            bool actual = gateway.IsSignedIn;

            // Assert
            Assert.AreEqual(expected, actual);
        }

        [TestCase(null, "")]
        [TestCase("", "")]
        [TestCase("player", "player")]
        public void PlayerId_GivenSdkValue_WhenRead_ThenNullIsNormalized(
            string playerId,
            string expected)
        {
            // Arrange
            var sdk = new FakeUnityAuthenticationSdk { PlayerId = playerId };
            var gateway = new UnityAuthenticationGateway(sdk);

            // Act
            string actual = gateway.PlayerId;

            // Assert
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Initialize_GivenSdkTask_WhenCalled_ThenDelegatesExactlyOnce()
        {
            // Arrange
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var sdk = new FakeUnityAuthenticationSdk { InitializeTask = completion.Task };
            var gateway = new UnityAuthenticationGateway(sdk);

            // Act
            Task operation = gateway.InitializeAsync();

            // Assert
            Assert.AreSame(completion.Task, operation);
            Assert.AreEqual(1, sdk.InitializeCalls);
        }

        [Test]
        public void SignInAnonymously_GivenSdkTask_WhenCalled_ThenDelegatesExactlyOnce()
        {
            // Arrange
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var sdk = new FakeUnityAuthenticationSdk { SignInAnonymouslyTask = completion.Task };
            var gateway = new UnityAuthenticationGateway(sdk);

            // Act
            Task operation = gateway.SignInAnonymouslyAsync();

            // Assert
            Assert.AreSame(completion.Task, operation);
            Assert.AreEqual(1, sdk.SignInAnonymouslyCalls);
        }

        [TestCase(null, false)]
        [TestCase("", false)]
        [TestCase("unity-account", true)]
        public async Task IsLinked_GivenUnityAccountId_WhenCalled_ThenReturnsExpectedValue(
            string unityAccountId,
            bool expected)
        {
            // Arrange
            var sdk = new FakeUnityAuthenticationSdk { UnityAccountId = unityAccountId };
            var gateway = new UnityAuthenticationGateway(sdk);

            // Act
            bool actual = await gateway.IsUnityAccountLinkedAsync();

            // Assert
            Assert.AreEqual(expected, actual);
            Assert.AreEqual(1, sdk.GetUnityAccountIdCalls);
        }

        [Test]
        public async Task Link_GivenSuccessfulSdkCall_WhenCalled_ThenReturnsCurrentPlayerId()
        {
            // Arrange
            var sdk = new FakeUnityAuthenticationSdk { PlayerId = "player" };
            var gateway = new UnityAuthenticationGateway(sdk);

            // Act
            AccountLinkResult result = await gateway.LinkWithUnityAsync("access-token");

            // Assert
            Assert.AreEqual(AccountLinkStatus.Success, result.Status);
            Assert.AreEqual("player", result.PlayerId);
            Assert.AreEqual("access-token", sdk.LastAccessToken);
            Assert.AreEqual(1, sdk.LinkWithUnityCalls);
        }

        [Test]
        public async Task Link_GivenAlreadyLinkedAuthenticationException_WhenCalled_ThenReturnsConflict()
        {
            // Arrange
            var sdk = new FakeUnityAuthenticationSdk
            {
                LinkException = CreateAuthenticationException(
                    AuthenticationErrorCodes.AccountAlreadyLinked,
                    "already linked")
            };
            var gateway = new UnityAuthenticationGateway(sdk);

            // Act
            AccountLinkResult result = await gateway.LinkWithUnityAsync("access-token");

            // Assert
            Assert.AreEqual(AccountLinkStatus.AlreadyLinked, result.Status);
            Assert.AreEqual("already linked", result.ErrorMessage);
            Assert.AreEqual(string.Empty, result.PlayerId);
        }

        [Test]
        public async Task Link_GivenOtherAuthenticationException_WhenCalled_ThenReturnsFailure()
        {
            // Arrange
            var sdk = new FakeUnityAuthenticationSdk
            {
                LinkException = CreateAuthenticationException(
                    AuthenticationErrorCodes.InvalidParameters,
                    "invalid token")
            };
            var gateway = new UnityAuthenticationGateway(sdk);

            // Act
            AccountLinkResult result = await gateway.LinkWithUnityAsync("access-token");

            // Assert
            Assert.AreEqual(AccountLinkStatus.Failed, result.Status);
            Assert.AreEqual("invalid token", result.ErrorMessage);
            Assert.AreEqual(string.Empty, result.PlayerId);
        }

        [Test]
        public async Task Link_GivenRequestFailedException_WhenCalled_ThenReturnsFailure()
        {
            // Arrange
            var sdk = new FakeUnityAuthenticationSdk
            {
                LinkException = new RequestFailedException(CommonErrorCodes.TransportError, "transport failed")
            };
            var gateway = new UnityAuthenticationGateway(sdk);

            // Act
            AccountLinkResult result = await gateway.LinkWithUnityAsync("access-token");

            // Assert
            Assert.AreEqual(AccountLinkStatus.Failed, result.Status);
            Assert.AreEqual("transport failed", result.ErrorMessage);
            Assert.AreEqual(string.Empty, result.PlayerId);
        }

        [Test]
        public void Unlink_GivenSdkTask_WhenCalled_ThenDelegatesExactlyOnce()
        {
            // Arrange
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var sdk = new FakeUnityAuthenticationSdk { UnlinkTask = completion.Task };
            var gateway = new UnityAuthenticationGateway(sdk);

            // Act
            Task operation = gateway.UnlinkUnityAsync();

            // Assert
            Assert.AreSame(completion.Task, operation);
            Assert.AreEqual(1, sdk.UnlinkCalls);
        }

        private static AuthenticationException CreateAuthenticationException(int errorCode, string message)
        {
            return (AuthenticationException)AuthenticationException.Create(errorCode, message);
        }

        /// <summary>
        /// Имитирует Unity Authentication SDK и фиксирует делегированные gateway-вызовы.
        /// </summary>
        private sealed class FakeUnityAuthenticationSdk : IUnityAuthenticationSdk
        {
            public bool IsSignedIn { get; set; }
            public string PlayerId { get; set; } = "player";
            public string UnityAccountId { get; set; }
            public Exception LinkException { get; set; }
            public Task InitializeTask { get; set; } = Task.CompletedTask;
            public Task SignInAnonymouslyTask { get; set; } = Task.CompletedTask;
            public Task LinkTask { get; set; } = Task.CompletedTask;
            public Task UnlinkTask { get; set; } = Task.CompletedTask;
            public int InitializeCalls { get; private set; }
            public int SignInAnonymouslyCalls { get; private set; }
            public int GetUnityAccountIdCalls { get; private set; }
            public int LinkWithUnityCalls { get; private set; }
            public int UnlinkCalls { get; private set; }
            public string LastAccessToken { get; private set; }

            public Task InitializeAsync()
            {
                InitializeCalls++;
                return InitializeTask;
            }

            public Task SignInAnonymouslyAsync()
            {
                SignInAnonymouslyCalls++;
                return SignInAnonymouslyTask;
            }

            public Task<string> GetUnityAccountIdAsync()
            {
                GetUnityAccountIdCalls++;
                return Task.FromResult(UnityAccountId);
            }

            public Task LinkWithUnityAsync(string accessToken)
            {
                LinkWithUnityCalls++;
                LastAccessToken = accessToken;
                return LinkException == null ? LinkTask : Task.FromException(LinkException);
            }

            public Task UnlinkUnityAsync()
            {
                UnlinkCalls++;
                return UnlinkTask;
            }
        }
    }
}
