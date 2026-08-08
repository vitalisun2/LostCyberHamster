namespace Assets.Scripts.GameEngine.Skins
{
    /// <summary>
    /// Описывает одно визуальное действие, синхронизированное с transform-анимацией.
    /// </summary>
    public readonly struct SkinActionContext
    {
        public SkinVisualAction Action { get; }
        public SkinVisualVariant Variant { get; }
        public SkinVisualOutcome Outcome { get; }
        public float Duration { get; }
        public float? ContactTime { get; }
        public long ActionId { get; }

        public SkinActionContext(
            SkinVisualAction action,
            SkinVisualVariant variant,
            SkinVisualOutcome outcome,
            float duration,
            float? contactTime,
            long actionId)
        {
            Action = action;
            Variant = variant;
            Outcome = outcome;
            Duration = duration;
            ContactTime = contactTime;
            ActionId = actionId;
        }

        public bool IsLoop => Action is SkinVisualAction.GroundRun or SkinVisualAction.RoofRun;
    }
}
