namespace LostCyberHamster.Editor.Testing
{
    /// <summary>Сценарий проверки облачных сохранений.</summary>
    public enum CloudSaveE2EScenario
    {
        /// <summary>Первое облачное сохранение.</summary>
        FirstCloudSave,

        /// <summary>Автоматическая отправка прогресса.</summary>
        AutomaticSynchronization,

        /// <summary>Повтор отложенной отправки.</summary>
        DeferredSynchronization,

        /// <summary>Восстановление существующего аккаунта.</summary>
        RestoreProgress,

        /// <summary>Обмен прогрессом между устройствами.</summary>
        MultipleDevices,

        /// <summary>Выбор облака при конфликте.</summary>
        ConflictChooseCloud,

        /// <summary>Выбор устройства при конфликте.</summary>
        ConflictChooseDevice,

        /// <summary>Отображение статуса синхронизации.</summary>
        SynchronizationStatus
    }
}
