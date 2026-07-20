using LostCyberHamster.UI;

namespace Assets.Scripts.Tutorial
{
    /// <summary>
    /// Описывает ожидаемое действие и условие перехода одного шага урока скина.
    /// </summary>
    public sealed class TutorialSkinStep
    {
        public TutorialSkinStep(
            TutorialSkinAction action,
            TutorialSkinPrompt prompt,
            ScreenEnum? completionSurface = null)
        {
            Action = action;
            Prompt = prompt;
            CompletionSurface = completionSurface;
        }

        public TutorialSkinAction Action { get; }
        public TutorialSkinPrompt Prompt { get; }
        public ScreenEnum? CompletionSurface { get; }
    }
}
