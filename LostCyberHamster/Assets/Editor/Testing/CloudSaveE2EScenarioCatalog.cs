using System;
using System.Collections.Generic;

namespace LostCyberHamster.Editor.Testing
{
    /// <summary>Хранит доступные сценарии и тексты для окна тестирования.</summary>
    public static class CloudSaveE2EScenarioCatalog
    {
        /// <summary>Сценарии в порядке отображения.</summary>
        private static readonly IReadOnlyList<CloudSaveE2EScenario> Scenarios =
            Array.AsReadOnly(new[]
            {
                CloudSaveE2EScenario.FirstCloudSave,
                CloudSaveE2EScenario.AutomaticSynchronization,
                CloudSaveE2EScenario.DeferredSynchronization,
                CloudSaveE2EScenario.RestoreProgress,
                CloudSaveE2EScenario.MultipleDevices,
                CloudSaveE2EScenario.ConflictChooseCloud,
                CloudSaveE2EScenario.ConflictChooseDevice,
                CloudSaveE2EScenario.SynchronizationStatus
            });

        /// <summary>Все сценарии в порядке отображения.</summary>
        public static IReadOnlyList<CloudSaveE2EScenario> All => Scenarios;

        /// <summary>Возвращает короткое название сценария.</summary>
        public static string GetTitle(CloudSaveE2EScenario scenario)
        {
            return scenario switch
            {
                CloudSaveE2EScenario.FirstCloudSave => "Первое облачное сохранение",
                CloudSaveE2EScenario.AutomaticSynchronization => "Автоматическая синхронизация",
                CloudSaveE2EScenario.DeferredSynchronization => "Отложенная синхронизация",
                CloudSaveE2EScenario.RestoreProgress => "Восстановление прогресса",
                CloudSaveE2EScenario.MultipleDevices => "Несколько устройств",
                CloudSaveE2EScenario.ConflictChooseCloud => "Конфликт: выбрать облако",
                CloudSaveE2EScenario.ConflictChooseDevice => "Конфликт: выбрать устройство",
                CloudSaveE2EScenario.SynchronizationStatus => "Статус синхронизации",
                _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null)
            };
        }

        /// <summary>Возвращает назначение сценария.</summary>
        public static string GetDescription(CloudSaveE2EScenario scenario)
        {
            return scenario switch
            {
                CloudSaveE2EScenario.FirstCloudSave =>
                    "Проверяет создание первого сохранения после привязки аккаунта.",
                CloudSaveE2EScenario.AutomaticSynchronization =>
                    "Проверяет отправку прогресса после обычного локального сохранения.",
                CloudSaveE2EScenario.DeferredSynchronization =>
                    "Проверяет повтор отправки после временной недоступности облака.",
                CloudSaveE2EScenario.RestoreProgress =>
                    "Проверяет вход в подготовленный аккаунт с другим облачным прогрессом.",
                CloudSaveE2EScenario.MultipleDevices =>
                    "Проверяет получение изменений, сделанных на другом устройстве.",
                CloudSaveE2EScenario.ConflictChooseCloud =>
                    "Проверяет конфликт при изменениях на устройстве и в облаке.",
                CloudSaveE2EScenario.ConflictChooseDevice =>
                    "Проверяет конфликт при изменениях на устройстве и в облаке.",
                CloudSaveE2EScenario.SynchronizationStatus =>
                    "Проверяет статус во время отправки нового прогресса.",
                _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null)
            };
        }

        /// <summary>Возвращает ожидаемый результат сценария.</summary>
        public static string GetExpectedResult(CloudSaveE2EScenario scenario)
        {
            return scenario switch
            {
                CloudSaveE2EScenario.FirstCloudSave =>
                    "В облаке появляется актуальный прогресс, статус подтверждает сохранение.",
                CloudSaveE2EScenario.AutomaticSynchronization =>
                    "Локальные изменения автоматически попадают в облако.",
                CloudSaveE2EScenario.DeferredSynchronization =>
                    "Ожидающий снимок отправляется после восстановления связи.",
                CloudSaveE2EScenario.RestoreProgress =>
                    "Локальные данные заменяются облачным прогрессом до завершения входа.",
                CloudSaveE2EScenario.MultipleDevices =>
                    "Клиент принимает более новую облачную версию.",
                CloudSaveE2EScenario.ConflictChooseCloud =>
                    "После выбора облака применяется облачный прогресс.",
                CloudSaveE2EScenario.ConflictChooseDevice =>
                    "После выбора устройства локальный прогресс записывается в облако.",
                CloudSaveE2EScenario.SynchronizationStatus =>
                    "В Settings видно отправку, а затем успешное сохранение.",
                _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null)
            };
        }
    }
}
