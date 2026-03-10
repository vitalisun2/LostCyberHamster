using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Assets.Scripts.Installers.Roots;
using Assets.Scripts.System;
using LoadingTasks;

namespace Assets.Scripts.Entry_Points.GameLoadingTasks
{
    [Serializable]
    public class InitDecorationsLoadingTask : ILoadingTaskSequence
    {
        public string Name => "Инициализация декораций";

        [UnityEngine.SerializeReference]
        private List<ILoadingTask> _children = new();
        public List<ILoadingTask> Children => _children;

        public async Task LoadAsync(Dictionary<string, object> bundle)
        {
            var environmentRoot = (EnvironmentRoot)bundle["environmentRoot"];
            DecorationSpawner.Instance.Init(environmentRoot);
        }
    }
}
