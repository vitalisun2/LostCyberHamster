using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LoadingTasks;
using UnityEngine;

namespace Assets.Scripts.Entry_Points.GameLoadingTasks
{
    [Serializable]
    public class RootTask : ILoadingTaskSequence
    {
        public string Name => "Корневая задача";

        // Use SerializeField to allow setting this in the Unity Editor
        [SerializeReference]
        private List<ILoadingTask> _children = new();
        public List<ILoadingTask> Children => _children;

        public async Task LoadAsync(Dictionary<string, object> bundle)
        {
            // Load logic here
        }
    }
}
