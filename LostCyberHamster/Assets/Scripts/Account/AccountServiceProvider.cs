namespace LostCyberHamster.Account
{
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
