using System;
using System.Collections.Generic;
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
                try
                {
                    await loadingTask.LoadAsync(bundle);
                }
                catch (Exception exception)
                {
                    // Локальная ошибка оставляет исходное сохранение для восстановления или обновления игры.
                    string key = exception is NotSupportedException ? "save_requires_newer_game" : "local_loading_failed";
                    string message = LocalizationManager.GetLocalizedString(key);
                    if (string.IsNullOrWhiteSpace(message) || message == key)
                        message = exception is NotSupportedException
                            ? "Сохранение создано новой версией игры. Обновите игру."
                            : "Не удалось загрузить данные. Перезапустите игру, чтобы повторить.";
                    _progressLabel.text = message;
                    Debug.LogError($"[Bootstrap] Local task '{loadingTask.Name}' failed ({exception.GetType().Name}).");
                    return;
                }
                SetLoadingProgress(loadingPercentage);
            }
        }

        private void SetLoadingProgress(int loadingPercentage)
        {
            _progressBar.value = loadingPercentage;
            _progressLabel.text = $"({loadingPercentage} %)";
        }
    }
}
