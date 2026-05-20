using System.Collections.Generic;
using System.Threading.Tasks;
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
        private const string TestLevelPrefsKey = "TestLevel_Address";

        public async Task LoadAsync(Dictionary<string, object> bundle)
        {
            // Check for test level override written by TestLevelLauncher
            if (PlayerPrefs.HasKey(TestLevelPrefsKey))
            {
                var levelAddress = PlayerPrefs.GetString(TestLevelPrefsKey);
                PlayerPrefs.DeleteKey(TestLevelPrefsKey);
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
