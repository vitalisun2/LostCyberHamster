using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LostCyberHamster.Account;
using NUnit.Framework;
using Unity.Services.Core;

namespace Assets.Tests.EditMode
{
    /// <summary>
    /// Проверяет lifecycle Unity Player Accounts gateway через детерминированные SDK и timeout ports.
    /// </summary>
    [Timeout(5000)]
    public sealed class UnityPlayerAccountGatewayTests
    {
        private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

        [Test]
        public void Constructor_GivenNullSdk_WhenCreated_ThenThrows()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new UnityPlayerAccountGateway(null, new FakeTimeout(), _timeout));
        }

        [Test]
        public void Constructor_GivenNullTimeout_WhenCreated_ThenThrows()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new UnityPlayerAccountGateway(new FakePlayerAccountSdk(), null, _timeout));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Constructor_GivenNonPositiveTimeout_WhenCreated_ThenThrows(int seconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new UnityPlayerAccountGateway(
                    new FakePlayerAccountSdk(),
                    new FakeTimeout(),
                    TimeSpan.FromSeconds(seconds)));
        }

        [Test]
        public async Task SignIn_GivenCachedSession_WhenFreshSignedInArrives_ThenReturnsOnlyFreshToken()
        {
            var sdk = new FakePlayerAccountSdk
            {
                IsSignedIn = true,
                AccessToken = "stale-token"
            };
            var gateway = CreateGateway(sdk, new FakeTimeout());

            Task<string> operation = gateway.SignInAndGetAccessTokenAsync();
            sdk.IsSignedIn = true;
            sdk.AccessToken = "fresh-token";
            sdk.RaiseSignedIn();
            string token = await operation;

            Assert.AreEqual("fresh-token", token);
            Assert.AreEqual(1, sdk.StartSignInCalls);
            Assert.AreEqual(1, sdk.SignOutCalls);
            AssertSubscriptionsReleased(sdk);
        }

        [TestCase(false, "token")]
        [TestCase(true, "")]
        [TestCase(true, null)]
        public async Task SignIn_GivenInvalidSignedInPayload_WhenEventArrives_ThenFailsAndResets(
            bool isSignedIn,
            string token)
        {
            var sdk = new FakePlayerAccountSdk();
            var gateway = CreateGateway(sdk, new FakeTimeout());

            Task<string> operation = gateway.SignInAndGetAccessTokenAsync();
            sdk.IsSignedIn = isSignedIn;
            sdk.AccessToken = token;
            sdk.RaiseSignedIn();

            await AssertThrowsAsync<InvalidOperationException>(operation);
            Assert.AreEqual(2, sdk.SignOutCalls);
            AssertSubscriptionsReleased(sdk);
        }

        [Test]
        public async Task SignIn_GivenSdkFailure_WhenEventArrives_ThenPropagatesAndResets()
        {
            var sdk = new FakePlayerAccountSdk();
            var gateway = CreateGateway(sdk, new FakeTimeout());
            var expected = new RequestFailedException(42, "oauth failed");

            Task<string> operation = gateway.SignInAndGetAccessTokenAsync();
            sdk.RaiseSignInFailed(expected);

            RequestFailedException actual = await AssertThrowsAsync<RequestFailedException>(operation);
            Assert.AreSame(expected, actual);
            Assert.AreEqual(2, sdk.SignOutCalls);
            AssertSubscriptionsReleased(sdk);
        }

        [Test]
        public async Task SignIn_GivenNullSdkFailure_WhenEventArrives_ThenReturnsMeaningfulError()
        {
            var sdk = new FakePlayerAccountSdk();
            var gateway = CreateGateway(sdk, new FakeTimeout());

            Task<string> operation = gateway.SignInAndGetAccessTokenAsync();
            sdk.RaiseSignInFailed(null);

            InvalidOperationException exception = await AssertThrowsAsync<InvalidOperationException>(operation);
            StringAssert.Contains("without an exception", exception.Message);
            AssertSubscriptionsReleased(sdk);
        }

        [Test]
        public async Task SignIn_GivenStartThrows_WhenStarted_ThenFailsAndUnsubscribes()
        {
            var sdk = new FakePlayerAccountSdk
            {
                StartSignInException = new InvalidOperationException("launch failed")
            };
            var gateway = CreateGateway(sdk, new FakeTimeout());

            InvalidOperationException exception = await AssertThrowsAsync<InvalidOperationException>(
                gateway.SignInAndGetAccessTokenAsync());

            Assert.AreEqual("launch failed", exception.Message);
            Assert.AreEqual(2, sdk.SignOutCalls);
            AssertSubscriptionsReleased(sdk);
        }

        [Test]
        public async Task SignIn_GivenStartNeverCompletes_WhenTimeoutWins_ThenTimesOutAndResets()
        {
            var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var sdk = new FakePlayerAccountSdk { StartSignInTask = start.Task };
            var timeout = new FakeTimeout();
            var timeoutSignal = timeout.EnqueuePending();
            var gateway = CreateGateway(sdk, timeout);

            Task<string> operation = gateway.SignInAndGetAccessTokenAsync();
            timeoutSignal.TrySetResult(true);

            await AssertThrowsAsync<TimeoutException>(operation);
            Assert.AreEqual(2, sdk.SignOutCalls);
            AssertSubscriptionsReleased(sdk);
        }

        [Test]
        public async Task SignIn_GivenCallbackNeverArrives_WhenTimeoutWins_ThenTimesOutAndResets()
        {
            var sdk = new FakePlayerAccountSdk();
            var timeout = new FakeTimeout();
            var timeoutSignal = timeout.EnqueuePending();
            var gateway = CreateGateway(sdk, timeout);

            Task<string> operation = gateway.SignInAndGetAccessTokenAsync();
            timeoutSignal.TrySetResult(true);

            await AssertThrowsAsync<TimeoutException>(operation);
            Assert.AreEqual(2, sdk.SignOutCalls);
            AssertSubscriptionsReleased(sdk);
        }

        [Test]
        public async Task SignIn_GivenConcurrentCallers_WhenFlowIsActive_ThenSharesSingleOperation()
        {
            var sdk = new FakePlayerAccountSdk();
            var gateway = CreateGateway(sdk, new FakeTimeout());

            Task<string> first = gateway.SignInAndGetAccessTokenAsync();
            Task<string> second = gateway.SignInAndGetAccessTokenAsync();
            sdk.IsSignedIn = true;
            sdk.AccessToken = "shared-token";
            sdk.RaiseSignedIn();
            string[] results = await Task.WhenAll(first, second);

            Assert.AreSame(first, second);
            CollectionAssert.AreEqual(new[] { "shared-token", "shared-token" }, results);
            Assert.AreEqual(1, sdk.StartSignInCalls);
            Assert.AreEqual(1, sdk.SignOutCalls);
            AssertSubscriptionsReleased(sdk);
        }

        [Test]
        public async Task SignIn_GivenSignedInBeforeStartTaskCompletes_WhenEventArrives_ThenCompletesSuccessfully()
        {
            var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var sdk = new FakePlayerAccountSdk { StartSignInTask = start.Task };
            var gateway = CreateGateway(sdk, new FakeTimeout());

            Task<string> operation = gateway.SignInAndGetAccessTokenAsync();
            sdk.IsSignedIn = true;
            sdk.AccessToken = "early-token";
            sdk.RaiseSignedIn();
            string token = await operation;

            Assert.AreEqual("early-token", token);
            AssertSubscriptionsReleased(sdk);
        }

        [Test]
        public async Task SignIn_GivenPreviousSuccess_WhenRetried_ThenStartsFreshFlow()
        {
            var sdk = new FakePlayerAccountSdk();
            var gateway = CreateGateway(sdk, new FakeTimeout());

            Task<string> first = gateway.SignInAndGetAccessTokenAsync();
            sdk.IsSignedIn = true;
            sdk.AccessToken = "first";
            sdk.RaiseSignedIn();
            await first;
            Task<string> second = gateway.SignInAndGetAccessTokenAsync();
            sdk.IsSignedIn = true;
            sdk.AccessToken = "second";
            sdk.RaiseSignedIn();

            Assert.AreEqual("second", await second);
            Assert.AreEqual(2, sdk.StartSignInCalls);
            Assert.AreEqual(2, sdk.SignOutCalls);
            AssertSubscriptionsReleased(sdk);
        }

        [Test]
        public async Task SignIn_GivenPreviousFailure_WhenRetried_ThenStartsFreshFlow()
        {
            var sdk = new FakePlayerAccountSdk();
            var gateway = CreateGateway(sdk, new FakeTimeout());

            Task<string> first = gateway.SignInAndGetAccessTokenAsync();
            sdk.RaiseSignInFailed(new RequestFailedException(42, "first failed"));
            await AssertThrowsAsync<RequestFailedException>(first);
            Task<string> second = gateway.SignInAndGetAccessTokenAsync();
            sdk.IsSignedIn = true;
            sdk.AccessToken = "retry-token";
            sdk.RaiseSignedIn();

            Assert.AreEqual("retry-token", await second);
            Assert.AreEqual(2, sdk.StartSignInCalls);
            Assert.AreEqual(3, sdk.SignOutCalls);
            AssertSubscriptionsReleased(sdk);
        }

        [Test]
        public async Task SignIn_GivenPreviousTimeout_WhenRetried_ThenStartsFreshFlow()
        {
            var sdk = new FakePlayerAccountSdk();
            var timeout = new FakeTimeout();
            var firstTimeout = timeout.EnqueuePending();
            timeout.EnqueueNeverCompleting();
            var gateway = CreateGateway(sdk, timeout);

            Task<string> first = gateway.SignInAndGetAccessTokenAsync();
            firstTimeout.TrySetResult(true);
            await AssertThrowsAsync<TimeoutException>(first);
            Task<string> second = gateway.SignInAndGetAccessTokenAsync();
            sdk.IsSignedIn = true;
            sdk.AccessToken = "retry-token";
            sdk.RaiseSignedIn();

            Assert.AreEqual("retry-token", await second);
            Assert.AreEqual(2, sdk.StartSignInCalls);
            Assert.AreEqual(3, sdk.SignOutCalls);
            AssertSubscriptionsReleased(sdk);
        }

        [Test]
        public async Task SignIn_GivenEarlySuccessAndLateStartFault_WhenRetried_ThenFreshFlowSucceeds()
        {
            var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var sdk = new FakePlayerAccountSdk { StartSignInTask = start.Task };
            var gateway = CreateGateway(sdk, new FakeTimeout());

            Task<string> first = gateway.SignInAndGetAccessTokenAsync();
            sdk.IsSignedIn = true;
            sdk.AccessToken = "early-token";
            sdk.RaiseSignedIn();
            Assert.AreEqual("early-token", await first);
            start.SetException(new InvalidOperationException("late launch failure"));
            sdk.StartSignInTask = Task.CompletedTask;

            Task<string> retry = gateway.SignInAndGetAccessTokenAsync();
            sdk.IsSignedIn = true;
            sdk.AccessToken = "retry-token";
            sdk.RaiseSignedIn();

            Assert.AreEqual("retry-token", await retry);
            Assert.AreEqual(2, sdk.StartSignInCalls);
            AssertSubscriptionsReleased(sdk);
        }

        [Test]
        public async Task SignIn_GivenTimeoutAndLateStartFault_WhenRetried_ThenFreshFlowSucceeds()
        {
            var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var sdk = new FakePlayerAccountSdk { StartSignInTask = start.Task };
            var timeout = new FakeTimeout();
            var timeoutSignal = timeout.EnqueuePending();
            timeout.EnqueueNeverCompleting();
            var gateway = CreateGateway(sdk, timeout);

            Task<string> first = gateway.SignInAndGetAccessTokenAsync();
            timeoutSignal.SetResult(true);
            await AssertThrowsAsync<TimeoutException>(first);
            start.SetException(new InvalidOperationException("late launch failure"));
            sdk.StartSignInTask = Task.CompletedTask;

            Task<string> retry = gateway.SignInAndGetAccessTokenAsync();
            sdk.IsSignedIn = true;
            sdk.AccessToken = "retry-token";
            sdk.RaiseSignedIn();

            Assert.AreEqual("retry-token", await retry);
            Assert.AreEqual(2, sdk.StartSignInCalls);
            AssertSubscriptionsReleased(sdk);
        }

        [Test]
        public async Task SignIn_GivenSignedInAndTimeoutCompleteTogether_WhenStarted_ThenSignedInWins()
        {
            var sdk = new FakePlayerAccountSdk
            {
                StartSignInAction = () =>
                {
                    // Event is raised synchronously in the same turn as the already-completed timeout.
                }
            };
            var timeout = new FakeTimeout();
            timeout.EnqueueCompleted();
            sdk.StartSignInAction = () =>
            {
                sdk.IsSignedIn = true;
                sdk.AccessToken = "winning-token";
                sdk.RaiseSignedIn();
            };
            var gateway = CreateGateway(sdk, timeout);

            string token = await gateway.SignInAndGetAccessTokenAsync();

            Assert.AreEqual("winning-token", token);
            AssertSubscriptionsReleased(sdk);
        }

        private static UnityPlayerAccountGateway CreateGateway(
            FakePlayerAccountSdk sdk,
            FakeTimeout timeout)
        {
            return new UnityPlayerAccountGateway(sdk, timeout, _timeout);
        }

        private static async Task<TException> AssertThrowsAsync<TException>(Task operation)
            where TException : Exception
        {
            try
            {
                await operation;
            }
            catch (TException exception)
            {
                return exception;
            }
            catch (Exception exception)
            {
                Assert.Fail(
                    $"Expected {typeof(TException).Name}, but got {exception.GetType().Name}: {exception.Message}");
            }

            Assert.Fail($"Expected {typeof(TException).Name}, but operation completed successfully.");
            return null;
        }

        private static void AssertSubscriptionsReleased(FakePlayerAccountSdk sdk)
        {
            Assert.AreEqual(sdk.SignedInAddCalls, sdk.SignedInRemoveCalls);
            Assert.AreEqual(sdk.SignInFailedAddCalls, sdk.SignInFailedRemoveCalls);
            Assert.AreEqual(0, sdk.SignedInSubscriberCount);
            Assert.AreEqual(0, sdk.SignInFailedSubscriberCount);
        }

        /// <summary>
        /// Управляет событиями и состоянием SDK-порта с точными счётчиками lifecycle-вызовов.
        /// </summary>
        private sealed class FakePlayerAccountSdk : IUnityPlayerAccountSdk
        {
            private Action _signedIn;
            private Action<RequestFailedException> _signInFailed;

            public event Action SignedIn
            {
                add
                {
                    SignedInAddCalls++;
                    _signedIn += value;
                }
                remove
                {
                    SignedInRemoveCalls++;
                    _signedIn -= value;
                }
            }

            public event Action<RequestFailedException> SignInFailed
            {
                add
                {
                    SignInFailedAddCalls++;
                    _signInFailed += value;
                }
                remove
                {
                    SignInFailedRemoveCalls++;
                    _signInFailed -= value;
                }
            }

            public bool IsSignedIn { get; set; }
            public string AccessToken { get; set; }
            public Task StartSignInTask { get; set; } = Task.CompletedTask;
            public Exception StartSignInException { get; set; }
            public Action StartSignInAction { get; set; }
            public int StartSignInCalls { get; private set; }
            public int SignOutCalls { get; private set; }
            public int SignedInAddCalls { get; private set; }
            public int SignedInRemoveCalls { get; private set; }
            public int SignInFailedAddCalls { get; private set; }
            public int SignInFailedRemoveCalls { get; private set; }
            public int SignedInSubscriberCount => _signedIn?.GetInvocationList().Length ?? 0;
            public int SignInFailedSubscriberCount => _signInFailed?.GetInvocationList().Length ?? 0;

            public Task StartSignInAsync()
            {
                StartSignInCalls++;
                if (StartSignInException != null)
                {
                    throw StartSignInException;
                }

                StartSignInAction?.Invoke();
                return StartSignInTask;
            }

            public void SignOut()
            {
                SignOutCalls++;
                IsSignedIn = false;
                AccessToken = string.Empty;
            }

            public void RaiseSignedIn()
            {
                _signedIn?.Invoke();
            }

            public void RaiseSignInFailed(RequestFailedException exception)
            {
                _signInFailed?.Invoke(exception);
            }
        }

        /// <summary>
        /// Возвращает заранее подготовленные timeout tasks без real-time ожидания.
        /// </summary>
        private sealed class FakeTimeout : IUnityPlayerAccountTimeout
        {
            private readonly Queue<Task> _tasks = new Queue<Task>();

            public Task WaitAsync(TimeSpan timeout)
            {
                return _tasks.Count > 0 ? _tasks.Dequeue() : NeverCompletingTask();
            }

            public TaskCompletionSource<bool> EnqueuePending()
            {
                var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _tasks.Enqueue(completion.Task);
                return completion;
            }

            public void EnqueueNeverCompleting()
            {
                _tasks.Enqueue(NeverCompletingTask());
            }

            public void EnqueueCompleted()
            {
                _tasks.Enqueue(Task.CompletedTask);
            }

            private static Task NeverCompletingTask()
            {
                return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously).Task;
            }
        }
    }
}
