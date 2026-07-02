namespace LostCyberHamster.Account
{
    public readonly struct AccountSnapshot
    {
        public static AccountSnapshot Unknown => new AccountSnapshot(
            AccountState.Unknown,
            string.Empty,
            false,
            false,
            string.Empty);

        public AccountSnapshot(
            AccountState state,
            string playerId,
            bool isSignedIn,
            bool isLinked,
            string errorMessage)
        {
            State = state;
            PlayerId = playerId ?? string.Empty;
            IsSignedIn = isSignedIn;
            IsLinked = isLinked;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public AccountState State { get; }
        public string PlayerId { get; }
        public bool IsSignedIn { get; }
        public bool IsLinked { get; }
        public string ErrorMessage { get; }

        public bool CanUseCloudSave => IsSignedIn && State != AccountState.Offline && State != AccountState.Error;
    }
}
