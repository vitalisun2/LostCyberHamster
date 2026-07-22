using System.Collections.Generic;
using System.Threading.Tasks;
using LoadingTasks;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;

namespace Assets.Scripts.Entry_Points.BootstrapLoadingTasks
{
    public sealed class InitUnityServicesLoadingTask : ILoadingTask
    {
        public string Name => "Инициализация Unity Gaming Services";

        public List<ILoadingTask> Children { get; } = new();

        /// <summary>
        /// Инициализирует Unity Gaming Services в development для Editor и Development Build,
        /// в production — для release build.
        /// </summary>
        public async Task LoadAsync(Dictionary<string, object> bundle)
        {
            // Выбирает окружение по типу запуска приложения.
            string environmentName = Application.isEditor || Debug.isDebugBuild
                ? "development"
                : "production";

            // Инициализирует UGS в выбранном окружении.
            var options = new InitializationOptions().SetEnvironmentName(environmentName);
            await UnityServices.InitializeAsync(options);
        }
    }
}
