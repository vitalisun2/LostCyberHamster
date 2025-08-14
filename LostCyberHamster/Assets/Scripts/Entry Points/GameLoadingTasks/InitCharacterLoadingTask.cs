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
            var hamster = GameObject.Instantiate(_characterPrefab,
                new Vector3(Consts.HamsterXPos, Consts.HamsterYPos, 0), Quaternion.identity, _environmentRoot.transform);

            AddGameListeners(hamster);
            HelpMethods.ApplyOverrideController(hamster);

            LevelController.Instance.LevelData.Hamster = hamster;
        }

        private void AddGameListeners(Hamster hamster)
        {
            var listeners = hamster.gameObject.GetComponentsInChildren<Listeners.IGameListener>();

            foreach (var listener in listeners)
            {
                LevelController.Instance.LevelData.GameManager.AddListener(listener);
            }
        }

    }
}
