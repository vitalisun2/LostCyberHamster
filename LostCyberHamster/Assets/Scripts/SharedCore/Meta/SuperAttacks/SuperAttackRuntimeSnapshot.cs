namespace Vues.GameCore
{
    /// <summary>Неизменяемое состояние текущей активации для HUD и обеих диагностических поверхностей.</summary>
    public readonly struct SuperAttackRuntimeSnapshot
    {
        public readonly int AbilityId, Level, RemainingCombinations, DestroyedCount, DropsCreated;
        public readonly long ActivationId;
        public readonly float Remaining, Duration;
        public readonly bool IsActive, IsFinishing;

        public SuperAttackRuntimeSnapshot(int abilityId, int level, long activationId, bool isActive,
            float remaining, float duration, int remainingCombinations = 0, bool isFinishing = false,
            int destroyedCount = 0, int dropsCreated = 0)
        {
            AbilityId = abilityId;
            Level = level;
            ActivationId = activationId;
            IsActive = isActive;
            Remaining = remaining;
            Duration = duration;
            RemainingCombinations = remainingCombinations;
            IsFinishing = isFinishing;
            DestroyedCount = destroyedCount;
            DropsCreated = dropsCreated;
        }
    }
}
