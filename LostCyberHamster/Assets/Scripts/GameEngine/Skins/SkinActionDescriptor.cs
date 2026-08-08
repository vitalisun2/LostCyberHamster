namespace Assets.Scripts.GameEngine.Skins
{
    /// <summary>
    /// Связывает семантическое visual-действие с transform-клипом — источником длительности.
    /// </summary>
    public readonly struct SkinActionDescriptor
    {
        public SkinVisualAction Action { get; }
        public SkinVisualVariant Variant { get; }
        public SkinVisualOutcome Outcome { get; }
        public string TransformClipName { get; }

        public SkinActionDescriptor(
            SkinVisualAction action,
            SkinVisualVariant variant,
            SkinVisualOutcome outcome,
            string transformClipName)
        {
            Action = action;
            Variant = variant;
            Outcome = outcome;
            TransformClipName = transformClipName;
        }
    }
}
