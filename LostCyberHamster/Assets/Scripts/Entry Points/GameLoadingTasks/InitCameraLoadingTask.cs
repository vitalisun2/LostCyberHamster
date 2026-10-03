using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Assets.Scripts.Gameplay;
using LoadingTasks;
using UnityEngine;

namespace Assets.Scripts.Entry_Points.GameLoadingTasks
{
    [Serializable]
    public class InitCameraLoadingTask : ILoadingTaskSequence
    {
        public string Name => "Инициализация камеры";

        // Дочерние задачи настраиваются в Unity Editor.
        [SerializeReference]
        private List<ILoadingTask> _children = new();
        public List<ILoadingTask> Children => _children;

        /// <summary>Применяет общие настройки игровой камеры и редактора фонов.</summary>
        public Task LoadAsync(Dictionary<string, object> bundle)
        {
            var gameCamera = (Camera)bundle["gameCamera"];
            LocationEnvironment.ConfigureCamera(gameCamera);
            return Task.CompletedTask;
        }
    }
}
