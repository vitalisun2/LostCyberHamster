using System.Collections.Generic;
using System.Threading.Tasks;
using LoadingTasks;
using Unity.Services.Core;

namespace Assets.Scripts.Entry_Points.BootstrapLoadingTasks
{
    public sealed class InitUnityServicesLoadingTask : ILoadingTask
    {
        public string Name => "Инициализация Unity Gaming Services";

        public List<ILoadingTask> Children { get; } = new();

        /// <summary>
        /// Инициализирует Unity Gaming Services до запуска зависимых сервисов.
        /// </summary>
        public async Task LoadAsync(Dictionary<string, object> bundle)
        {
            await UnityServices.InitializeAsync();
        }
    }
}
