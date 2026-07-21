# Cloud Save: план рефакторинга

## Сигнал

`CloudSyncService` одновременно управляет lifecycle, очередью, версиями, retry, восстановлением и конфликтами. Поток уже трудно быстро прочитать и удержать в голове. Это практический сигнал смешения ответственности; фиксированного лимита строк не требуется.

## Что есть сейчас

- `GameDataManager` — локальный `PlayerData` и его атомарная замена.
- `PlayerProgressCommitter` — локальный commit и событие checkpoint.
- `CloudSaveSnapshotCodec` — снимок и сериализация.
- `CloudPendingSnapshotStore` — один durable pending и одна подтверждённая версия в `PlayerPrefs`.
- `ICloudSaveGateway` / `UnityCloudSaveGateway` — чтение и условная запись UGS.
- `CloudSyncService` — вся синхронизация и runtime-состояние.
- `ExistingAccountRestoreCoordinator` — связывает вход в аккаунт с облачным восстановлением.
- `CloudSaveConflictCoordinator` — показывает конфликт и передаёт выбор игрока.

## Ответственности `CloudSyncService`

| Блок | Методы и состояние |
|---|---|
| Lifecycle и аккаунт | конструктор, `Dispose`, `OnCurrentGuestLinked`, `OnAccountStateChanged`, `OnApplicationResumed` |
| Capture и revision | `UploadFirstSnapshotAsync`, `OnCheckpointCommitted`, `_nextLocalRevision` |
| Pending-очередь | `UploadPendingSnapshotAsync`, `CompleteConfirmedSnapshot`, `RebasePendingTo`, `RetainActiveSnapshot`, `_pendingSnapshot`, `_firstSnapshotAwaitingConfirmation`, `_isSnapshotUploadActive` |
| Refresh и cloud version | `TryUploadPendingForCurrentAccount`, `RefreshCloudOnlyAsync`, `RestoreCurrentCloudVersion`, `SetCurrentCloudVersion`, `CurrentCloudVersion`, `_isCloudRefreshActive` |
| Восстановление аккаунта | `LoadExistingAccountAsync`, `TryRestoreValidatedPlayerData`, `DiscardPendingForOwner` |
| Конфликт | оба `ResolveConflict...`, `SetConflict`, `CurrentConflict`, `_isConflictResolutionActive` |
| Классификация веток | ветвление внутри `UploadPendingSnapshotAsync`, missing-cloud marker, `AreEquivalent` |

## Доказанные проблемы

### P1. Refresh может откатить уже загруженный прогресс

`RefreshCloudOnlyAsync` начинает чтение старой версии (`498–504`). Пока оно ждёт, checkpoint создаёт pending и запускает upload (`444–468`). Upload записывает новую версию и очищает pending (`315–321`, `392–395`). Затем старый refresh видит `pending == null` (`505`), применяет старые данные и сохраняет старую confirmed revision (`531–550`).

Нужен один сериализованный поток cloud-операций. Проверка только `_pendingSnapshot` недостаточна: active snapshot временно лежит в локальной переменной.

### P1. Один foreign pending можно потерять

Pending другого `PlayerId` не отправляется (`485–490`), но следующий checkpoint текущего игрока без предупреждения заменяет и память, и единственный durable slot (`454–460`). Несинхронизированный снимок прошлого владельца исчезает.

Нужно согласовать продуктовую политику: запрещать смену владельца при pending, очищать явно либо хранить pending по `PlayerId`.

### P2. Операции живут после `Dispose`

`Dispose` только отписывает события (`298–305`). Уже запущенные fire-and-forget операции (`427`, `468`, `488`, `494`, `560`) могут позднее изменить данные. Нужна отмена или generation token.

### Поддерживаемость

- `CloudPendingSnapshotStore` назван как pending-store, но хранит ещё confirmed version.
- Статический store и статические события создают скрытые зависимости и глобальное состояние в тестах.
- Имена `RetryPendingFirstSnapshotAsync`, `HasPendingFirstSnapshot` и логи `First snapshot` уже не описывают общую очередь checkpoint.
- Для будущих `Offline` и `Error` gateway/service пока не имеют явной классификации ошибок.

## Варианты композиции

### Вариант A — минимальный

- Оставить `CloudSyncService` фасадом и владельцем runtime-состояния.
- Добавить один serialized operation pump для upload, refresh и resolve.
- Вынести чистый classifier: `Upload`, `AlreadyApplied`, `Conflict`, `Retain`, `Recreate`, `OwnerMismatch`.
- Заменить статический store инъецируемым `CloudSyncStateStore`, не деля pending и confirmed на лишние классы.

### Вариант B — очередь как отдельная ответственность

- `CloudSyncService` принимает события аккаунта, checkpoint и lifecycle.
- `PendingSnapshotQueue` владеет capture, active/pending, revision и durable pending.
- Единый sync engine владеет cloud operation gate, confirmed version и конфликтом.
- Classifier остаётся чистым.

Важно: upload, refresh и resolve нельзя разнести между независимыми владельцами состояния.

### Вариант C — единый sync engine

- Тонкий `CloudSyncService` остаётся публичным API.
- Один `CloudSyncEngine` последовательно обрабатывает команды `Checkpoint`, `Refresh`, `Retry`, `ChooseCloud`, `ChooseLocal`.
- Store и gateway остаются портами.

Это яснее как state machine, но сложнее вариантов A/B. Решение пока не принято.

## Безопасный порядок

1. Зафиксировать тестами гонку refresh/upload и смену владельца с pending.
2. Сериализовать cloud-операции и добавить отмену после `Dispose`.
3. Согласовать политику foreign pending.
4. Вынести чистую классификацию веток.
5. Сделать state store инъецируемым и уточнить имена API.
6. Повторно оценить читаемость. Только затем выбирать дополнительное разделение классов.
7. После стабилизации добавить sync status и классификацию ошибок.
