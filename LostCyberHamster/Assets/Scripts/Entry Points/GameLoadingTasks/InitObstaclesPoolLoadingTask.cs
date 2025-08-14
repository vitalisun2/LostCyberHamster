using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.Common.Models;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Installers.Roots;
using Assets.Scripts.System;
using LoadingTasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Assets.Scripts.Entry_Points.GameLoadingTasks
{
    [Serializable]
    public class InitObstaclesPoolLoadingTask : ILoadingTaskSequence
    {
        public string Name => "Инициализация пула препятствий";

        [SerializeReference]
        private List<ILoadingTask> _children = new();
        public List<ILoadingTask> Children => _children;

        private EnvironmentRoot _environmentRoot;

        public async Task LoadAsync(Dictionary<string, object> bundle)
        {
            _environmentRoot = (EnvironmentRoot)bundle["environmentRoot"];

           ObstacleSpawner.Instance.Init(_environmentRoot);
        }

       
    }

}
