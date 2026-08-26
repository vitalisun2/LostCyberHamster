using System.Collections.Generic;
using System.Threading.Tasks;
using Assets.Scripts.System;
using GameManagement;
using LoadingTasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Entry_Points.BootstrapLoadingTasks
{
    internal class LoadMainMenuLoadingTask : ILoadingTaskSequence
    {
        public string Name { get; } = "Загрузка главного меню";
        public List<ILoadingTask> Children { get; }

        private const string MenuScene = "Menu";
        private const string GameScene = "Game";
        public async Task LoadAsync(Dictionary<string, object> bundle)
        {
            // Потребляем адрес один раз; отдельный test-level marker живёт до выхода из Play Mode.
            if (PlayerPrefs.HasKey(AutomationRuntimePrefs.TestLevelAddressOverrideKey))
            {
                var levelAddress = PlayerPrefs.GetString(
                    AutomationRuntimePrefs.TestLevelAddressOverrideKey);
                PlayerPrefs.DeleteKey(AutomationRuntimePrefs.TestLevelAddressOverrideKey);
                PlayerPrefs.Save();

                if (!string.IsNullOrEmpty(levelAddress))
                {
                    GameDataManager.PlayerData.CurrentLevel = levelAddress;
                    await SceneManager.LoadSceneAsync(GameScene);
                    return;
                }
            }

            await SceneManager.LoadSceneAsync(MenuScene);
        }
    }
}
