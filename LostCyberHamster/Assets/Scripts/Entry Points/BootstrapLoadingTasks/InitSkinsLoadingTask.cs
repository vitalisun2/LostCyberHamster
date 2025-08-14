using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LoadingTasks;

namespace Assets.Scripts.Entry_Points.BootstrapLoadingTasks
{
    public class InitSkinsLoadingTask : ILoadingTaskSequence
    {
        public string Name => "Инициализация скинов";

        public List<ILoadingTask> Children { get; }

        public InitSkinsLoadingTask()
        {
        }

        public async Task LoadAsync(Dictionary<string, object> bundle)
        {
            await SkinManager.Init();
        }

    }
}
