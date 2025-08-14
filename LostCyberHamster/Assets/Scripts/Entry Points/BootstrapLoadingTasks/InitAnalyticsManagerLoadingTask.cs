using System.Collections.Generic;
using System.Threading.Tasks;
using Vues.GameCore;
using LoadingTasks;

namespace Assets.Scripts.Entry_Points.BootstrapLoadingTasks
{
    public class InitAnalyticsManagerLoadingTask : ILoadingTaskSequence
    {
        public string Name => "Инициализация системы аналитики";

        public List<ILoadingTask> Children { get; }

        public InitAnalyticsManagerLoadingTask()
        {
        }

        public async Task LoadAsync(Dictionary<string, object> bundle)
        {
            await AnalyticsManager.InitializeAsync();
        }
    }
}
