namespace LostCyberHamster.Account
{
    /// <summary>
    /// Хранит подменяемую глобальную точку доступа к текущей реализации account-сервиса.
    /// </summary>
    public static class AccountServiceProvider
    {
        private static IAccountService _current;

        public static IAccountService Current => _current ?? AccountService.Instance;

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
