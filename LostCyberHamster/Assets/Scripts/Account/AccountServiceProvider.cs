namespace LostCyberHamster.Account
{
    /// <summary>
    /// Хранит подменяемую глобальную точку доступа к текущей реализации account-сервиса.
    /// </summary>
    public static class AccountServiceProvider
    {
        private static readonly IAccountService _default = new AccountService(
            new UnityAuthenticationGateway(),
            new UnityPlayerAccountGateway());

        private static IAccountService _current;

        public static IAccountService Current => _current ?? _default;

        internal static void SetForTests(IAccountService service)
        {
            _current = service;
        }

        internal static void ResetForTests()
        {
            _current = null;
        }
    }
}
