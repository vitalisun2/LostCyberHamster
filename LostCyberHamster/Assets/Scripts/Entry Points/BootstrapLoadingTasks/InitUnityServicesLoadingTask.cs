using System.Collections.Generic;
using System.Threading.Tasks;
using LoadingTasks;
using Assets.Scripts.Online;

namespace Assets.Scripts.Entry_Points.BootstrapLoadingTasks
{
    public sealed class InitUnityServicesLoadingTask : ILoadingTask
    {
        public string Name => "Инициализация Unity Gaming Services";

        public List<ILoadingTask> Children { get; } = new();

        /// <summary>
        /// Регистрирует фоновую инициализацию UGS с восстановлением после ошибки.
        /// </summary>
        public Task LoadAsync(Dictionary<string, object> bundle)
        {
            OnlineServicesCoordinator.StartUnityServices();
            return Task.CompletedTask;
        }
    }
}
