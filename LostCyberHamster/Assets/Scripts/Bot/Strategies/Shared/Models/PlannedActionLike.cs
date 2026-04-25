namespace Assets.Scripts.Bot.Strategies.Shared.Models
{
    /// <summary>
    /// Минимальная форма action для jump retained validation.
    /// </summary>
    internal sealed class PlannedActionLike
    {
        public PlannedActionLike(float triggerX, float postFireWorldShift)
        {
            TriggerX = triggerX;
            PostFireWorldShift = postFireWorldShift;
        }

        public float TriggerX { get; }
        public float PostFireWorldShift { get; }
    }
}
