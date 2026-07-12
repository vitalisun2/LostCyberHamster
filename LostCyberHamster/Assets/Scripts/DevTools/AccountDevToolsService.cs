#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Reflection;
using System.Threading.Tasks;
using LostCyberHamster.Account;
using Unity.Services.Authentication;
using Unity.Services.Authentication.PlayerAccounts;
using Unity.Services.Core;
using UnityEngine;

namespace Assets.Scripts.DevTools
{
    /// <summary>
    /// Собирает account-диагностику и выполняет account/SDK-команды для DEV-меню.
    /// </summary>
    internal sealed class AccountDevToolsService
    {
        private const string _settingsResourceName = "UnityPlayerAccountSettings";
        private const string _dashboardUrl = "https://cloud.unity.com/home";

        private readonly IAccountService _accountService;

        public AccountDevToolsService(IAccountService accountService)
        {
            _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
        }

        public AccountSnapshot Snapshot => _accountService.Snapshot;

        public bool IsLocallyReadyForPlayerAccounts =>
            !string.IsNullOrWhiteSpace(Application.cloudProjectId) &&
            !string.IsNullOrWhiteSpace(GetPlayerAccountClientId());

        /// <summary>
        /// Возвращает короткий пользовательский статус без SDK-терминов.
        /// </summary>
        public string GetHumanStatusText()
        {
            AccountSnapshot snapshot = Snapshot;
            if (snapshot.State == AccountState.Error)
            {
                return "Ошибка авторизации";
            }

            if (snapshot.State == AccountState.Offline)
            {
                return "Нет соединения";
            }

            if (snapshot.IsLinked)
            {
                return "Аккаунт привязан";
            }

            if (snapshot.IsSignedIn)
            {
                return "Гость";
            }

            return "Сессия не готова";
        }

        /// <summary>
        /// Возвращает локально проверяемые prerequisites и явно отделяет их от Dashboard-конфигурации.
        /// </summary>
        public string GetReadinessText()
        {
            string cloudProjectId = Application.cloudProjectId;
            string clientId = GetPlayerAccountClientId();
            string servicesState = UnityServices.State.ToString();
            string requiredPlatform = GetRequiredDashboardPlatform();
            bool localReady = !string.IsNullOrWhiteSpace(cloudProjectId) && !string.IsNullOrWhiteSpace(clientId);

            return $"cloudProjectId: {DisplayValue(cloudProjectId)}\n" +
                   $"UPA Client ID: {DisplayValue(clientId)}\n" +
                   $"Unity Services: {servicesState}\n" +
                   $"Локальная готовность: {(localReady ? "Да" : "Нет")}\n" +
                   $"Dashboard provider / {requiredPlatform}: проверить вручную";
        }

        /// <summary>
        /// Возвращает runtime-состояние обеих независимых сессий: UGS Authentication и Player Accounts OAuth.
        /// </summary>
        public string GetSessionText()
        {
            bool authSignedIn = TryGetAuthenticationValue(service => service.IsSignedIn);
            bool sessionTokenExists = TryGetAuthenticationValue(service => service.SessionTokenExists);
            bool playerAccountSignedIn = TryGetPlayerAccountValue(service => service.IsSignedIn);
            AccountSnapshot snapshot = Snapshot;

            return $"AccountState: {snapshot.State}\n" +
                   $"UGS PlayerId: {DisplayValue(snapshot.PlayerId)}\n" +
                   $"UGS signed in: {YesNo(snapshot.IsSignedIn)}\n" +
                   $"UGS cached token: {YesNo(sessionTokenExists)}\n" +
                   $"UPA OAuth session: {YesNo(playerAccountSignedIn)}\n" +
                   $"IsLinked: {YesNo(snapshot.IsLinked)}\n" +
                   $"Последняя ошибка: {DisplayValue(snapshot.ErrorMessage)}\n" +
                   $"SDK UGS signed in: {YesNo(authSignedIn)}";
        }

        /// <summary>
        /// Восстанавливает cached UGS identity или создаёт гостя, если cached credentials отсутствуют.
        /// </summary>
        public Task<AccountSnapshot> EnsureSessionAsync()
        {
            return _accountService.EnsureSignedInAsync();
        }

        /// <summary>
        /// Перечитывает linked-state текущего UGS-игрока.
        /// </summary>
        public Task<AccountSnapshot> RefreshAsync()
        {
            return _accountService.RefreshLinkStateAsync();
        }

        /// <summary>
        /// Запускает реальный Unity Player Accounts flow без автоматической смены Player ID при конфликте.
        /// </summary>
        public Task<AccountLinkResult> LinkAsync()
        {
            return _accountService.LinkUnityAccountAsync();
        }

        /// <summary>
        /// Отвязывает Unity Player Account от текущего UGS-игрока.
        /// </summary>
        public Task<AccountSnapshot> UnlinkAsync()
        {
            return _accountService.UnlinkUnityAccountAsync();
        }

        /// <summary>
        /// Завершает активную UGS-сессию, но сохраняет cached credentials для восстановления того же Player ID.
        /// </summary>
        public async Task<AccountSnapshot> SignOutUgsKeepingCredentialsAsync()
        {
            if (AuthenticationService.Instance.IsSignedIn)
            {
                AuthenticationService.Instance.SignOut();
            }

            return await _accountService.RefreshLinkStateAsync();
        }

        /// <summary>
        /// Очищает только локальную Player Accounts OAuth-сессию, не меняя UGS Player ID и linked-state.
        /// </summary>
        public void SignOutPlayerAccount()
        {
            if (PlayerAccountService.Instance.IsSignedIn)
            {
                PlayerAccountService.Instance.SignOut();
            }
        }

        /// <summary>
        /// Удаляет локальные UGS credentials; следующий anonymous sign-in создаст новый Player ID.
        /// </summary>
        public async Task<AccountSnapshot> ClearCachedIdentityAsync()
        {
            if (AuthenticationService.Instance.IsSignedIn)
            {
                AuthenticationService.Instance.SignOut(true);
            }
            else if (AuthenticationService.Instance.SessionTokenExists)
            {
                AuthenticationService.Instance.ClearSessionToken();
            }

            return await _accountService.RefreshLinkStateAsync();
        }

        /// <summary>
        /// Открывает Unity Cloud Dashboard; выбор текущего проекта и provider остаётся административным действием пользователя.
        /// </summary>
        public void OpenDashboard()
        {
            Application.OpenURL(_dashboardUrl);
        }

        private static string GetPlayerAccountClientId()
        {
            ScriptableObject settings = Resources.Load<ScriptableObject>(_settingsResourceName);
            PropertyInfo clientIdProperty = settings?.GetType().GetProperty(
                "ClientId",
                BindingFlags.Instance | BindingFlags.Public);

            return clientIdProperty?.GetValue(settings) as string ?? string.Empty;
        }

        private static bool TryGetAuthenticationValue(Func<IAuthenticationService, bool> getter)
        {
            try
            {
                return getter(AuthenticationService.Instance);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetPlayerAccountValue(Func<IPlayerAccountService, bool> getter)
        {
            try
            {
                return getter(PlayerAccountService.Instance);
            }
            catch
            {
                return false;
            }
        }

        private static string DisplayValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "—" : value;
        }

        private static string YesNo(bool value)
        {
            return value ? "Да" : "Нет";
        }

        private static string GetRequiredDashboardPlatform()
        {
#if UNITY_EDITOR
            return "PC для Play Mode";
#elif UNITY_ANDROID
            return "Android";
#elif UNITY_IOS
            return "iOS";
#else
            return Application.platform.ToString();
#endif
        }
    }
}
#endif
