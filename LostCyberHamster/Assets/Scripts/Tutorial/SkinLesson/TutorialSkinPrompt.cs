using LostCyberHamster.UI;

namespace Assets.Scripts.Tutorial
{
    /// <summary>
    /// Описывает экран, UI-цель и текст одного шага урока скина.
    /// </summary>
    public sealed class TutorialSkinPrompt
    {
        public TutorialSkinPrompt(
            ScreenEnum surface,
            TutorialSkinTarget target,
            string instruction,
            TutorialFocusShape shape)
        {
            Surface = surface;
            Target = target;
            Instruction = instruction;
            Shape = shape;
        }

        public ScreenEnum Surface { get; }
        public TutorialSkinTarget Target { get; }
        public string Instruction { get; }
        public TutorialFocusShape Shape { get; }
    }
}
