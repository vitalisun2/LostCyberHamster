namespace LostCyberHamster.Account
{
    /// <summary>
    /// Хранит неизменяемый снимок текущего состояния аккаунта и UGS-сессии.
    /// </summary>
    public readonly struct AccountSnapshot
    {
        private readonly string _playerId;
        private readonly string _errorMessage;

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
            _playerId = playerId ?? string.Empty;
            IsSignedIn = isSignedIn;
            IsLinked = isLinked;
            _errorMessage = errorMessage ?? string.Empty;
        }

        public AccountState State { get; }
        public string PlayerId => _playerId ?? string.Empty;
        public bool IsSignedIn { get; }
        public bool IsLinked { get; }
        public string ErrorMessage => _errorMessage ?? string.Empty;

        public bool CanUseCloudSave => IsSignedIn &&
                                       (State == AccountState.Guest || State == AccountState.Linked);
    }
}
