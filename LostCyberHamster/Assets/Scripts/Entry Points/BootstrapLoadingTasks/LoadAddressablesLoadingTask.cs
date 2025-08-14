using System.Collections.Generic;
using System.Threading.Tasks;
using Vues.GameCore;
using GameManagement;
using LoadingTasks;
using UnityEngine.AddressableAssets;

namespace Assets.Scripts.Entry_Points.BootstrapLoadingTasks
{
    public class LoadAddressablesLoadingTask : ILoadingTaskSequence
    {
        public string Name => "Загрузка ассетов";
        public List<ILoadingTask> Children { get; }

        private List<string> _addressablesKeys = new List<string>
        {
            "music_test1",
            "BackgroundScreenSprite"
        };

        public LoadAddressablesLoadingTask()
        {

        }

        public async Task LoadAsync(Dictionary<string, object> bundle)
        {
            foreach (var key in _addressablesKeys)
            {
                await Addressables.InitializeAsync().Task;
            }
        }
    }
}
