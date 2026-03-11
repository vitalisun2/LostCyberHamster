using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Assets.Scripts.Common;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Installers.Roots;
using Assets.Scripts.System;
using LoadingTasks;
using UnityEngine;

namespace Assets.Scripts.Entry_Points.GameLoadingTasks
{
    [Serializable]
    public class InitCharacterLoadingTask : ILoadingTaskSequence
    {
        public string Name => "Создание персонажа";

        [SerializeReference]
        private List<ILoadingTask> _children = new();
        public List<ILoadingTask> Children => _children;

        private EnvironmentRoot _environmentRoot;
        private Hamster _characterPrefab;

        public async Task LoadAsync(Dictionary<string, object> bundle)
        {
            _characterPrefab = (Hamster)bundle["characterPrefab"];
            _environmentRoot = (EnvironmentRoot)bundle["environmentRoot"];

            CreateHamster();
        }

        private void CreateHamster()
        {
            DebugManager.DiagLog("[InitCharacterTask] Creating hamster...");
            var hamster = GameObject.Instantiate(_characterPrefab,
                new Vector3(Consts.HamsterXPos, Consts.HamsterYPos, 0), Quaternion.identity, _environmentRoot.transform);

            AddGameListeners(hamster);
            HelpMethods.ApplyOverrideController(hamster);

            LevelController.Instance.LevelData.Hamster = hamster;
            DebugManager.DiagLog("[InitCharacterTask] Hamster created and assigned.");
        }

        private void AddGameListeners(Hamster hamster)
        {
            var gameManager = LevelController.Instance.LevelData.GameManager;
            var listeners = hamster.gameObject.GetComponentsInChildren<Listeners.IGameListener>();

            DebugManager.DiagLog($"[InitCharacterTask] Adding {listeners.Length} listeners. GameState={gameManager.State}");
            foreach (var listener in listeners)
            {
                gameManager.AddListener(listener);
            }

            // If game already started (e.g. test level without intro), fire OnStart for late listeners
            if (gameManager.State == GameState.PLAYING)
            {
                foreach (var listener in listeners)
                {
                    if (listener is Listeners.IGameStartListener startListener)
                        startListener.OnStart();
                }
            }
        }

    }
}
