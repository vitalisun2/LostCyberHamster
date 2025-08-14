using System.Collections.Generic;
using System.Threading.Tasks;
using Assets.Scripts.System;
using LoadingTasks;

namespace Assets.Scripts.Entry_Points.BootstrapLoadingTasks
{
    public class InitLocationsListLoadingTask : ILoadingTaskSequence
    {
        public string Name => "Инициализация списка локаций";
        public List<ILoadingTask> Children { get; }

        public InitLocationsListLoadingTask()
        {

        }

        public async Task LoadAsync(Dictionary<string, object> bundle)
        {
            await LevelManager.Init();
        }
    }
}
