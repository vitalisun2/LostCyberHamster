using Assets.Scripts.GameManagerLogic;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public class UiToMenuMechanics
    {
        private readonly VisualElement _root;
        private string _sceneName = "Menu";

        public UiToMenuMechanics(VisualElement root, GameManager gameManager)
        {
            _root = root;
        }

        public void Subscribe()
        {
            _root.RegisterCallback<ClickEvent>(OnButtonClick);
        }

        public void Unsubscribe()
        {
            _root.UnregisterCallback<ClickEvent>(OnButtonClick);
        }

        private void OnButtonClick(ClickEvent e)
        {
            SceneManager.LoadScene(_sceneName);
        }
    }
}
