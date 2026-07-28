using System.Collections.Generic;
using System.Threading.Tasks;
using LoadingTasks;
using Vues.GameCore;

namespace Assets.Scripts.Entry_Points.BootstrapLoadingTasks
{
    /// <summary>
    /// Инициализирует данные суперударов.
    /// </summary>
    public sealed class InitSuperAttacksLoadingTask : ILoadingTaskSequence
    {
        /// <summary>
        /// Отображаемое имя задачи загрузки.
        /// </summary>
        public string Name => "Инициализация суперударов";

        /// <summary>
        /// Дочерние задачи, выполняемые после инициализации каталога.
        /// </summary>
        public List<ILoadingTask> Children { get; } = new List<ILoadingTask>();

        /// <summary>
        /// Загружает каталог в SuperAttackService.
        /// </summary>
        public async Task LoadAsync(Dictionary<string, object> bundle)
        {
            await SuperAttackService.InitAsync();
        }
    }
}
