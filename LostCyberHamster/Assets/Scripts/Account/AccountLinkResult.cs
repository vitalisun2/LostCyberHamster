namespace LostCyberHamster.Account
{
    public readonly struct AccountLinkResult
    {
        private AccountLinkResult(AccountLinkStatus status, string playerId, string errorMessage)
        {
            Status = status;
            PlayerId = playerId ?? string.Empty;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public AccountLinkStatus Status { get; }
        public string PlayerId { get; }
        public string ErrorMessage { get; }

        public bool IsSuccess => Status == AccountLinkStatus.Success;

        /// <summary>
        /// Создаёт успешный результат для UGS-игрока с указанным идентификатором.
        /// </summary>
        public static AccountLinkResult Success(string playerId)
        {
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
