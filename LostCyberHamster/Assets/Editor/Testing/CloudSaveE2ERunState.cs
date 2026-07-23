namespace LostCyberHamster.Editor.Testing
{
    /// <summary>Состояние запуска Cloud Save E2E-сценария.</summary>
    public enum CloudSaveE2ERunState
    {
        /// <summary>Сценарий не запущен.</summary>
        Idle,

        /// <summary>Выполняется автоматический шаг.</summary>
        Running,

        /// <summary>Ожидается действие пользователя.</summary>
        WaitingForUser,

        /// <summary>Сценарий успешно завершён.</summary>
        Passed,

        /// <summary>Сценарий завершён с ошибкой.</summary>
        Failed,

        /// <summary>Сценарий отменён.</summary>
        Cancelled
    }
}
