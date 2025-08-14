using Assets.Scripts.GameManagerLogic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public class UiPauseMechanics
    {
        private readonly VisualElement _root;
        private readonly GameManager _gameManager;

        public UiPauseMechanics(VisualElement root, GameManager gameManager)
        {
            _root = root;
            _gameManager = gameManager;
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
            _gameManager.Pause();
        }
    }
}
