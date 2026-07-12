namespace LostCyberHamster.Account
{
    /// <summary>
    /// Описывает результат попытки привязки внешнего аккаунта к UGS-игроку.
    /// </summary>
    public readonly struct AccountLinkResult
    {
        private readonly string _playerId;
        private readonly string _errorMessage;

        private AccountLinkResult(AccountLinkStatus status, string playerId, string errorMessage)
        {
            Status = status;
            _playerId = playerId ?? string.Empty;
            _errorMessage = errorMessage ?? string.Empty;
        }

        public AccountLinkStatus Status { get; }
        public string PlayerId => _playerId ?? string.Empty;
        public string ErrorMessage => _errorMessage ?? string.Empty;

        public bool IsSuccess => Status == AccountLinkStatus.Success;

        /// <summary>
        /// Создаёт неопределённый результат для ещё не выполненной операции.
        /// </summary>
        public static AccountLinkResult Unknown(string errorMessage = "")
        {
            return new AccountLinkResult(AccountLinkStatus.Unknown, string.Empty, errorMessage);
        }

        /// <summary>
        /// Создаёт успешный результат для UGS-игрока с указанным идентификатором.
        /// </summary>
        public static AccountLinkResult Success(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return Failed("UGS player ID is empty.");
            }

            return new AccountLinkResult(AccountLinkStatus.Success, playerId, string.Empty);
        }

        /// <summary>
        /// Создаёт результат конфликта, когда внешний аккаунт уже принадлежит другому UGS-игроку.
        /// </summary>
        public static AccountLinkResult AlreadyLinked(string errorMessage)
        {
            return new AccountLinkResult(AccountLinkStatus.AlreadyLinked, string.Empty, errorMessage);
        }

        /// <summary>
        /// Создаёт неуспешный результат с диагностическим описанием причины.
        /// </summary>
        public static AccountLinkResult Failed(string errorMessage)
        {
            return new AccountLinkResult(AccountLinkStatus.Failed, string.Empty, errorMessage);
        }
    }
}
