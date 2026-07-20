using UnityEngine.UIElements;

namespace Assets.Scripts.TutorialOld
{
    public static class TutorialUiTargetResolver
    {
        public static VisualElement Resolve(VisualElement root, TutorialUiTarget target)
        {
            return target switch
            {
                TutorialUiTarget.WinHomeButton => root.Q<VisualElement>("btn__home"),
                TutorialUiTarget.HomeCharacterButton => root.Q<VisualElement>("btn_character"),
                TutorialUiTarget.SkinNextButton => root.Q<VisualElement>("btn-skin-next"),
                TutorialUiTarget.SkinChangeButton => root.Q<VisualElement>("skin-btn-change"),
                _ => null
            };
        }
    }
}
