# Cloud Save: план упрощения

## Текущая структура

```text
CloudSave/
├─ CloudSyncService.cs
├─ AccountRestore/   ExistingAccountRestoreCoordinator, ExistingAccountRestoreResult
├─ Conflicts/        CloudSaveConflictService, CloudSaveConflictModel
├─ Gateway/          interface, Unity adapter, read/write results
├─ Snapshots/        CloudSaveSnapshotDto, codec, restorer
├─ Uploads/          SnapshotUploadService, PendingSnapshotStore
└─ Versions/         ConfirmedVersionStore
```

`Model`, `Serialization` и `Shared` объединены в `Snapshots`; namespace сохранён единым.

## MVP

- **Снимок:** весь `PlayerData` сохраняется одной версией. Частичного merge нет.
- **Локальный commit:** всегда происходит первым. Сеть не блокирует игру.
- **Pending:** последний неподтверждённый снимок хранится локально для конкретного `PlayerId`.
- **Upload и retry:** связанный аккаунт отправляет pending; ошибка оставляет его для resume/retry.
- **Restore:** вход в существующий аккаунт целиком применяет его облачный снимок.
- **Refresh:** resume и смена устройства подтягивают cloud-only изменения, не затирая новый local pending.
- **Conflict:** если local и cloud изменились от общей базы, игрок выбирает целиком одну ветку.
- **Safety:** смена аккаунта, устаревшие async-ответы, чужой owner, повреждённый snapshot и потерянный server acknowledgement не меняют данные молча.

## Минимальный алгоритм

1. `PlayerProgressCommitter` фиксирует согласованный локальный checkpoint.
2. `CloudSyncService` создаёт immutable snapshot для текущего связанного `PlayerId`.
3. `SnapshotUploadService` сохраняет newest pending до сетевого вызова.
4. Gateway загружает текущую cloud version.
5. Сервис выбирает одну ветку: already applied, direct upload, retain/retry, owner mismatch или conflict.
6. Успешный upload подтверждает точный active snapshot, обновляет server revision и сразу отправляет более новый pending.
7. Restore/refresh/conflict-cloud применяют snapshot только после owner check, validation и повторной lifecycle-проверки.
8. Conflict-local перечитывает latest server revision и целиком записывает local snapshot.

Так сохраняются KISS-правила продукта: один snapshot, один local-first путь, один durable pending на владельца, явный выбор только при настоящем конфликте.

## Карта реализации

| Блок | Код | Основные тесты |
|---|---|---|
| Snapshot | `CloudSaveSnapshotDto`, `CloudSaveSnapshotCodec.Capture/Serialize/Deserialize/RestorePlayerData` | round-trip полного `PlayerData`; snapshot не меняется вместе с source |
| UGS boundary | `ICloudSaveGateway`, `UnityCloudSaveGateway.LoadSnapshotAsync/SaveSnapshotAsync`, read/write results | fake gateway в service tests; write-lock и SDK остаются внутри adapter |
| Pending и upload | `SnapshotUploadService`, `PendingSnapshotStore` | retry того же snapshot; restart; newest pending; per-owner isolation; lost acknowledgement |
| Confirmed version | `ConfirmedVersionStore`, `CloudSyncService.RestoreCurrentCloudVersion/SetCurrentCloudVersion` | restore revision; matching base; missing known base |
| Оркестрация | `CloudSyncService` account/checkpoint/resume handlers, upload, refresh, restore | linked account; offline retry; resume; cloud-only refresh; account owner guards |
| Restore данных | `CloudSaveSnapshotRestorer.TryRestore`, `GameDataManager.ReplacePlayerData` | valid/repairable snapshot; corrupt/unusable snapshot; load failure |
| Conflict | `CloudSaveConflictService`, `CloudSaveConflictModel`, `CloudSaveConflictCoordinator` | divergence; re-choice after cloud changed; choose cloud; choose local |
| Existing account | `ExistingAccountRestoreCoordinator`, `ExistingAccountRestoreResult` | valid restore; missing snapshot restores original guest |
| DI/UI | `ProjectInstaller`, `MenuEntryPoint`, Settings и conflict modal | compile/DI gate; UI использует только facade и DTO |

## Аудит production types

| Type | Решение | Причина и impact удаления |
|---|---|---|
| `CloudSyncService` | **SIMPLIFY** | Нужный публичный facade и orchestrator. Удаление ломает account/checkpoint/resume execution flow. Внутренний flow пока распределён между несколькими owners состояния. |
| `CloudSaveConflictService` | **KEEP / SIMPLIFY** | Владеет detection, состоянием и выбором конфликта. Нужен `CloudSyncService`; success bookkeeping частично остаётся во facade. |
| `CloudSaveConflictModel` | **KEEP** | Даёт UI независимые local/cloud copies. Удаление вернёт mutable runtime snapshots в modal. |
| `SnapshotUploadService` | **KEEP / SIMPLIFY** | Владеет active/pending, retry, first marker, owner и local revision. Удаление вернёт queue state в orchestrator. |
| `PendingSnapshotStore` | **KEEP** | Per-player encrypted durable pending. Merge с confirmed store смешает разные данные и recovery lifecycle. |
| `ConfirmedVersionStore` | **KEEP** | Per-player server revision/time для base и refresh. Без него restart теряет подтверждённую базу. |
| `ExistingAccountRestoreCoordinator` | **KEEP** | Связывает account switch transaction с принятием cloud restore. Это отдельный use case, не технический wrapper. |
| `ExistingAccountRestoreResult` | **KEEP** | Передаёт UI точную причину отказа. `bool` потеряет полезные исходы. |
| `ICloudSaveGateway` | **KEEP** | Граница UGS и test seam. Без неё domain flow зависит от SDK. |
| `UnityCloudSaveGateway` | **KEEP** | Единственный UGS adapter и владелец write-lock semantics. |
| `CloudSaveReadResult` | **KEEP** | Snapshot плюс server metadata для load/conflict. |
| `CloudSaveWriteResult` | **KEEP** | Подтверждённые metadata без nullable snapshot. Merge с read ухудшит контракт. |
| `CloudSaveSnapshotDto` | **KEEP** | Центральный полный cloud contract. |
| `CloudSaveSnapshotCodec` | **KEEP** | Один capture/serialization contract для gateway, stores, conflict UI и tests. |
| `CloudSaveSnapshotRestorer` | **KEEP** | Общая validation/repair/restore логика трёх apply-flow. Удаление создаст дубли. |

У всех 15 типов есть production consumer. Доказанного кандидата на полное удаление типа сейчас нет.

## Выполненное упрощение

1. **Один lifecycle owner.** `CloudSyncService` владеет одним `CancellationTokenSource`. Смена account state и `Dispose` отменяют все старые cloud-операции; `CloudSaveConflictService` использует тот же token и больше не хранит свою generation.
2. **Один upload drain.** `SnapshotUploadService.DrainPendingAsync` последовательно отправляет active и newest pending. Рекурсивный повтор из `CloudSyncService` удалён.
3. **Говорящие pending-методы.** `SetPendingSnapshot` и `SetFirstPendingSnapshot` заменили boolean mode flag.
4. **Единое оформление.** Production Cloud Save и затронутые Account/DI классы оформлены по `.github/prompts/oformi-class.prompt.md` без изменения поведения.

## Осознанно оставлено

- **Conflict completion.** Выбор ветки остаётся в conflict service, а pending/version bookkeeping — в `CloudSyncService`. Объединение смешало бы владельцев состояния; конфликт очищается только после успешного bookkeeping.
- **Cloud version и owner.** Пара полей остаётся простой и явной. Отдельная model ради их объединения добавит тип и не уменьшит проверки.
- **Public `GetStorageKey`.** Editor tests вызывают API из отдельной assembly, а runtime asmdef отсутствует. Сужение потребует лишней assembly infrastructure.
- **Комментарии и логи.** Удалялись только доказанные повторы; lifecycle guards и диагностические причины сохранены.

Не упрощать: owner checks, проверки после каждого cloud `await`, immutable active snapshot, per-player pending, durable retry, two-step missing-cloud recovery, validation/repair, conflict re-check перед выбором. UGS Cloud Save не даёт реальной отмены текущих SDK calls, поэтому эти guards обязательны.

## Минимальная целевая архитектура

- `CloudSyncService`: facade, account/checkpoint/resume routing, один lifecycle cancellation owner, confirmed cloud state.
- `SnapshotUploadService`: один последовательный pending drain и durable queue rules.
- `CloudSaveConflictService`: conflict detection, immutable branches, choice transaction.
- `CloudSaveSnapshotRestorer`: единый безопасный apply.
- Gateway, codec и два stores остаются узкими портами.

## Результат

Выполнены доказанные упрощения: один lifecycle owner и один upload loop. Новые production types, очереди и state machine не добавлены. Все 15 Cloud Save типов сохраняют отдельную ответственность и live consumers.
