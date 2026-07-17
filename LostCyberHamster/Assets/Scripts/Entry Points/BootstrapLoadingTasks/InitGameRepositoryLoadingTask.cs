using System.Collections.Generic;
using System.Threading.Tasks;
using Assets.Scripts.System;
using GameManagement;
using LoadingTasks;

namespace Assets.Scripts.Entry_Points.BootstrapLoadingTasks
{
    public class InitGameRepositoryLoadingTask : ILoadingTaskSequence
    {
        public string Name => "Загрузка данных";
        public List<ILoadingTask> Children { get; }

        public InitGameRepositoryLoadingTask()
        {
        }

        public async Task LoadAsync(Dictionary<string, object> bundle)
        {
            await LevelCatalogRuntimeConfigurator.ApplyInspectorOverrideAsync(forceRebuild: true);

            await GameDataManager.LoadDataAsync();

            GameDataManager.LoadSettings();

            await LevelCatalogRuntimeConfigurator.ApplyInspectorOverrideAsync();

        }
    }
}
