using System;
using System.Threading.Tasks;
using Unity.Services.Authentication.PlayerAccounts;
using Unity.Services.Core;

namespace LostCyberHamster.Account
{
    /// <summary>
    /// Предоставляет gateway минимальный порт к Unity Player Accounts SDK без прямой зависимости от static service locator.
    /// </summary>
    internal interface IUnityPlayerAccountSdk
    {
        event Action SignedIn;
        event Action<RequestFailedException> SignInFailed;

        bool IsSignedIn { get; }
        string AccessToken { get; }

        Task StartSignInAsync();
        void SignOut();
    }

    /// <summary>
    /// Предоставляет заменяемую границу ожидания для детерминированной проверки таймаутов gateway.
    /// </summary>
    internal interface IUnityPlayerAccountTimeout
    {
        Task WaitAsync(TimeSpan timeout);
    }

    /// <summary>
    /// Адаптирует static PlayerAccountService к порту gateway.
    /// </summary>
    internal sealed class UnityPlayerAccountSdk : IUnityPlayerAccountSdk
    {
        public event Action SignedIn
        {
            add => PlayerAccountService.Instance.SignedIn += value;
            remove => PlayerAccountService.Instance.SignedIn -= value;
        }

        public event Action<RequestFailedException> SignInFailed
        {
            add => PlayerAccountService.Instance.SignInFailed += value;
            remove => PlayerAccountService.Instance.SignInFailed -= value;
        }

        public bool IsSignedIn => PlayerAccountService.Instance.IsSignedIn;
        public string AccessToken => PlayerAccountService.Instance.AccessToken;

        public Task StartSignInAsync()
        {
            return PlayerAccountService.Instance.StartSignInAsync();
        }

        public void SignOut()
        {
            PlayerAccountService.Instance.SignOut();
        }
    }

    /// <summary>
    /// Реализует рабочий таймаут через системный планировщик задач.
    /// </summary>
    internal sealed class UnityPlayerAccountTimeout : IUnityPlayerAccountTimeout
    {
        public Task WaitAsync(TimeSpan timeout)
        {
            return Task.Delay(timeout);
        }
    }

    /// <summary>
    /// Запускает свежий интерактивный Unity Player Accounts flow и возвращает access token только после SignedIn.
    /// </summary>
    internal sealed class UnityPlayerAccountGateway : IUnityPlayerAccountGateway
    {
        private static readonly TimeSpan DefaultSignInTimeout = TimeSpan.FromMinutes(4);

        private readonly object _singleFlightLock = new object();
        private readonly IUnityPlayerAccountSdk _sdk;
        private readonly IUnityPlayerAccountTimeout _timeout;
        private readonly TimeSpan _signInTimeout;

        private Task<string> _activeSignIn;

        internal UnityPlayerAccountGateway()
            : this(new UnityPlayerAccountSdk(), new UnityPlayerAccountTimeout(), DefaultSignInTimeout)
        {
        }

        internal UnityPlayerAccountGateway(
            IUnityPlayerAccountSdk sdk,
            IUnityPlayerAccountTimeout timeout,
            TimeSpan signInTimeout)
        {
            _sdk = sdk ?? throw new ArgumentNullException(nameof(sdk));
            _timeout = timeout ?? throw new ArgumentNullException(nameof(timeout));

            if (signInTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(signInTimeout), "Sign-in timeout must be positive.");
            }

            _signInTimeout = signInTimeout;
        }

        /// <summary>
        /// Возвращает общий результат текущего входа либо запускает новый интерактивный flow.
        /// </summary>
        public Task<string> SignInAndGetAccessTokenAsync()
        {
            lock (_singleFlightLock)
            {
                if (_activeSignIn == null || _activeSignIn.IsCompleted)
                {
                    _activeSignIn = RunFreshSignInAsync();
                }

                return _activeSignIn;
            }
        }

        private async Task<string> RunFreshSignInAsync()
        {
            var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var completedSuccessfully = false;

            void HandleSignedIn()
            {
                var accessToken = _sdk.AccessToken;
                if (!_sdk.IsSignedIn || string.IsNullOrEmpty(accessToken))
                {
                    completion.TrySetException(
                        new InvalidOperationException("Unity Player Accounts reported SignedIn without a valid access token."));
                    return;
                }

                completion.TrySetResult(accessToken);
            }

            void HandleSignInFailed(RequestFailedException exception)
            {
                completion.TrySetException(
                    exception != null
                        ? (Exception)exception
                        : new InvalidOperationException("Unity Player Accounts sign-in failed without an exception."));
            }

            // Сбрасываем потенциально закэшированную сессию до подписки и запуска нового OAuth flow.
            _sdk.SignOut();

            try
            {
                _sdk.SignedIn += HandleSignedIn;
                _sdk.SignInFailed += HandleSignInFailed;

                // Единый таймаут ограничивает и запуск браузера, и ожидание пользовательского callback.
                var timeoutTask = _timeout.WaitAsync(_signInTimeout);
                var startSignInTask = _sdk.StartSignInAsync();
                var startCompletedTask = await Task.WhenAny(startSignInTask, completion.Task, timeoutTask);

                if (completion.Task.IsCompleted)
                {
                    var earlyAccessToken = await completion.Task;
                    completedSuccessfully = true;
                    return earlyAccessToken;
                }

                if (startCompletedTask != startSignInTask && !startSignInTask.IsCompleted)
                {
                    throw CreateTimeoutException();
                }

                // StartSignInAsync подтверждает только открытие браузера; результат входа приходит событием.
                await startSignInTask;
                var completedTask = await Task.WhenAny(completion.Task, timeoutTask);

                if (completedTask != completion.Task && !completion.Task.IsCompleted)
                {
                    throw CreateTimeoutException();
                }

                var accessToken = await completion.Task;
                completedSuccessfully = true;
                return accessToken;
            }
            finally
            {
                // События SDK принадлежат только одному запуску и не должны переживать его завершение.
                _sdk.SignedIn -= HandleSignedIn;
                _sdk.SignInFailed -= HandleSignInFailed;

                if (!completedSuccessfully)
                {
                    ResetSdkStateWithoutMaskingFailure();
                }
            }
        }

        private TimeoutException CreateTimeoutException()
        {
            return new TimeoutException(
                $"Unity Player Accounts sign-in did not complete within {_signInTimeout.TotalMinutes:0.#} minutes.");
        }

        private void ResetSdkStateWithoutMaskingFailure()
        {
            try
            {
                _sdk.SignOut();
            }
            catch
            {
                // Ошибка очистки не должна скрывать исходную ошибку входа или таймаут.
            }
        }
    }
}
