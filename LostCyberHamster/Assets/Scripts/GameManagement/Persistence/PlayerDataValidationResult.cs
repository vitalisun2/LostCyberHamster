namespace GameManagement
{
    public sealed class PlayerDataValidationResult
    {
        private PlayerDataValidationResult(PlayerDataValidationStatus status, string reason)
        {
            Status = status;
            Reason = reason;
        }

        public PlayerDataValidationStatus Status { get; }
        public string Reason { get; }

        public static PlayerDataValidationResult Valid()
        {
            return new PlayerDataValidationResult(PlayerDataValidationStatus.Valid, string.Empty);
        }

        public static PlayerDataValidationResult Repairable(string reason)
        {
            return new PlayerDataValidationResult(PlayerDataValidationStatus.Repairable, reason);
        }

        public static PlayerDataValidationResult Rejected(string reason)
        {
            return new PlayerDataValidationResult(PlayerDataValidationStatus.Rejected, reason);
        }
    }
}
