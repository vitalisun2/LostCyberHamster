using LostCyberHamster.UI;

namespace Assets.Scripts.TutorialOld
{
    public sealed class TutorialUiPrompt
    {
        public TutorialUiPrompt(
            ScreenEnum surface,
            TutorialUiTarget target,
            string instruction,
            TutorialFocusShape shape)
        {
            Surface = surface;
            Target = target;
            Instruction = instruction;
            Shape = shape;
        }

        public ScreenEnum Surface { get; }
        public TutorialUiTarget Target { get; }
        public string Instruction { get; }
        public TutorialFocusShape Shape { get; }
    }
}
