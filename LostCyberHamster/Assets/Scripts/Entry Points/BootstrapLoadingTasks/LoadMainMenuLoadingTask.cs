using System.Collections.Generic;
using System.Threading.Tasks;
using LoadingTasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using Vues.GameCore;

namespace Assets.Scripts.Entry_Points.BootstrapLoadingTasks
{
    internal class LoadMainMenuLoadingTask : ILoadingTaskSequence
    {
        public string Name { get; } = "Загрузка главного меню";
        public List<ILoadingTask> Children { get; }
        private string _sceneName = "Menu";

        public async Task LoadAsync(Dictionary<string, object> bundle)
        {
            await SceneManager.LoadSceneAsync(_sceneName);
        }
    }
}
