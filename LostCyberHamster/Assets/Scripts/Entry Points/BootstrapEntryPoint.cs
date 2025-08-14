using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LoadingTasks;
using UnityEngine;
using UnityEngine.UIElements;
using Zenject;

namespace Assets.Scripts.Entry_Points
{
    public class BootstrapEntryPoint : MonoBehaviour
    {
        private List<ILoadingTask> _loadingTasks;
        [SerializeField]
        private UIDocument _uiDocument;

        private ProgressBar _progressBar;

        Dictionary<string, object> bundle = new();

        private void Awake()
        {
            _progressBar = _uiDocument.rootVisualElement.Q<ProgressBar>("loading_task__progress");
        }

        [Inject]
        private void Construct(List<ILoadingTask> loadingTasks)
        {
            _loadingTasks = loadingTasks;
            //Debug.Log($"Loading tasks count: {_loadingTasks.Count}");
        }

        private async void Start()
        {
            int i = 1;
            foreach (var loadingTask in _loadingTasks)
            {
                Debug.Log($"Loading task {loadingTask.Name} started.");
                var loadingPercentage = (int)(i++ / (float)_loadingTasks.Count * 100);
                await loadingTask.LoadAsync(bundle);
                _progressBar.value = loadingPercentage;
                _progressBar.title = $"({loadingPercentage} %)";

                Debug.Log($"Loading task {loadingTask.Name} completed.");
                // await Task.Delay(1000);
            }
        }
    }
}
