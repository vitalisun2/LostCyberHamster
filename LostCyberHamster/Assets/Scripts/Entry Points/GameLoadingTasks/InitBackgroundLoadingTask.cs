using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Installers.Roots;
using Assets.Scripts.System;
using LoadingTasks;
using UnityEngine;
using Zenject;

namespace Assets.Scripts.Entry_Points.GameLoadingTasks
{
    [Serializable]
    public class InitBackgroundLoadingTask : ILoadingTaskSequence
    {
        public string Name => "Инициализация фона";

        [SerializeReference]
        private List<ILoadingTask> _children = new();
        public List<ILoadingTask> Children => _children;

        private EnvironmentRoot _environmentRoot;

        public async Task LoadAsync(Dictionary<string, object> bundle)
        {
            _environmentRoot = (EnvironmentRoot)bundle["environmentRoot"];

            InitBackgrounds();
        }

        private void InitBackgrounds()
        {
            var backgroundPrefab = LevelController.Instance.LevelData.BackgroundPrefab;

            var backgroundWidth = backgroundPrefab.GetComponentInChildren<SpriteRenderer>().bounds.size.x;

            var firstBackgroundGameObject = GameObject.Instantiate(backgroundPrefab, new Vector3(0, Consts.BackgroundYPos, 0), Quaternion.identity,
                _environmentRoot.transform);

            var secondBackGroundGameObject = GameObject.Instantiate(backgroundPrefab, new Vector3(backgroundWidth, Consts.BackgroundYPos, 0), Quaternion.identity,
                _environmentRoot.transform);

            var firstBackground = firstBackgroundGameObject.GetComponent<Background>();
            var secondBackGround = secondBackGroundGameObject.GetComponent<Background>();

            LevelController.Instance.LevelData.GameManager.AddListener(firstBackground);
            LevelController.Instance.LevelData.GameManager.AddListener(secondBackGround);
        }
    }
}
