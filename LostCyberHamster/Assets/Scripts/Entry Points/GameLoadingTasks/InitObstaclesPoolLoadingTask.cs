using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Assets.Scripts.Installers.Roots;
using Assets.Scripts.System;
using LoadingTasks;
using UnityEngine;

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
        private ObstacleSpawner _obstacleSpawner;

        public Task LoadAsync(Dictionary<string, object> bundle)
        {
            _environmentRoot = (EnvironmentRoot)bundle["environmentRoot"];
            _obstacleSpawner = (ObstacleSpawner)bundle["obstacleSpawner"];

            _obstacleSpawner.Init(_environmentRoot);
            return Task.CompletedTask;
        }
    }
}
