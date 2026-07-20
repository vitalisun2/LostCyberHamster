using LostCyberHamster.UI;

namespace Assets.Scripts.TutorialOld
{
    public sealed class TutorialUiStep
    {
        public TutorialUiStep(
            TutorialMetaStage stage,
            TutorialUiAction action,
            TutorialUiPrompt prompt,
            ScreenEnum? completionSurface = null)
        {
            Stage = stage;
            Action = action;
            Prompt = prompt;
            CompletionSurface = completionSurface;
        }

        public TutorialMetaStage Stage { get; }
        public TutorialUiAction Action { get; }
        public TutorialUiPrompt Prompt { get; }
        public ScreenEnum? CompletionSurface { get; }
        public bool WaitsForSurfaceCompletion => CompletionSurface.HasValue;

        public bool IsCompletedBySurface(ScreenEnum surface)
        {
            return CompletionSurface.HasValue && CompletionSurface.Value == surface;
        }
    }
}
