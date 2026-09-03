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
        private Label _progressLabel;

        Dictionary<string, object> bundle = new();

        private void Awake()
        {
            VisualElement root = _uiDocument.rootVisualElement;
            _progressBar = root.Q<ProgressBar>("loading_task__progress");
            _progressLabel = root.Q<Label>("loading_task__progress-label");
        }

        [Inject]
        private void Construct(List<ILoadingTask> loadingTasks)
        {
            _loadingTasks = loadingTasks;
       }

        private async void Start()
        {
            int i = 1;
            foreach (var loadingTask in _loadingTasks)
            {
                var loadingPercentage = (int)(i++ / (float)_loadingTasks.Count * 100);
                await loadingTask.LoadAsync(bundle);
                SetLoadingProgress(loadingPercentage);

               // await Task.Delay(1000);
            }
        }

        private void SetLoadingProgress(int loadingPercentage)
        {
            _progressBar.value = loadingPercentage;
            _progressLabel.text = $"({loadingPercentage} %)";
        }
    }
}
