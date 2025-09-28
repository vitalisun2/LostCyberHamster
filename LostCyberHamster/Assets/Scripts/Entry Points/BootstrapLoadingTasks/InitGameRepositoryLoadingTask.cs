using System.Collections.Generic;
using System.Threading.Tasks;
using Assets.Scripts.System;
using Vues.GameCore;
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
            await LevelCatalogRuntimeConfigurator.ApplyInspectorOverrideAsync();

            GameDataManager.InitializeAsync();
            await GameDataManager.LoadDataAsync();

            GameDataManager.LoadSettings();

            await LevelCatalogRuntimeConfigurator.ApplyInspectorOverrideAsync();

            GameDataManager.ApplyFeatureFlags();

            MoneyStorage.Init(GameDataManager.PlayerData.Money);
            CrystalStorage.Init(GameDataManager.PlayerData.Crystals);
        }
    }
}
