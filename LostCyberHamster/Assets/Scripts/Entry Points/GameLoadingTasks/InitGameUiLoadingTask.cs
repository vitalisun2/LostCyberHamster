using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Installers.Roots;
using Assets.Scripts.System;
using LoadingTasks;
using LostCyberHamster.UI;
using Unity.VisualScripting;
using UnityEngine;

namespace Assets.Scripts.Entry_Points.GameLoadingTasks
{
    [Serializable]
    public class InitGameUiLoadingTask : ILoadingTaskSequence
    {
        public string Name => "Инициализация UI";

        [SerializeReference]
        private List<ILoadingTask> _children = new();
        public List<ILoadingTask> Children => _children;

        private UiRoot _uiRoot;

        public async Task LoadAsync(Dictionary<string, object> bundle)
        {
            _uiRoot = (UiRoot)bundle["uiRoot"];

            var gameUi = _uiRoot.AddComponent<GameUi>();
            await gameUi.Construct();
            AddGameListeners(gameUi);
        }

        private void AddGameListeners(GameUi gameUi)
        {
            var listeners = gameUi.gameObject.GetComponentsInChildren<Listeners.IGameListener>();

            foreach (var listener in listeners) {
                LevelController.Instance.LevelData.GameManager.AddListener(listener);
            }
        }
    }
}
