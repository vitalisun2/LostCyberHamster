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

        public static AccountLinkResult Success(string playerId)
        {
            return new AccountLinkResult(AccountLinkStatus.Success, playerId, string.Empty);
        }

        public static AccountLinkResult AlreadyLinked(string errorMessage)
        {
            return new AccountLinkResult(AccountLinkStatus.AlreadyLinked, string.Empty, errorMessage);
        }

        public static AccountLinkResult Failed(string errorMessage)
        {
            return new AccountLinkResult(AccountLinkStatus.Failed, string.Empty, errorMessage);
        }
    }
}
