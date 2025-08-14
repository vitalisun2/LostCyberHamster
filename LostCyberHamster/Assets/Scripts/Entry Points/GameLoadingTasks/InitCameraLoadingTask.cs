using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LoadingTasks;
using UnityEngine;

namespace Assets.Scripts.Entry_Points.GameLoadingTasks
{
    [Serializable]
    public class InitCameraLoadingTask : ILoadingTaskSequence
    {
        public string Name => "Инициализация камеры";

        // Use SerializeField to allow setting this in the Unity Editor
        [SerializeReference]
        private List<ILoadingTask> _children = new();
        public List<ILoadingTask> Children => _children;

        public async Task LoadAsync(Dictionary<string, object> bundle)
        {
            Camera.main.orthographicSize = Consts.CameraSize;
            Camera.main.transform.position = Consts.CameraPosition;
        }
    }
}
