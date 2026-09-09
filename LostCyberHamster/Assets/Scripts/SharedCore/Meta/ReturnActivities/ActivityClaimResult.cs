namespace Vues.GameCore.ReturnActivities
{
    /// <summary>Различает успешную выдачу, повтор и отказ изменять предъявленный профиль.</summary>
    public enum ActivityClaimResult
    {
        Claimed, AlreadyClaimed, StaleContext, Unavailable, SaveFailed, RecoveryRequired
    }
}
