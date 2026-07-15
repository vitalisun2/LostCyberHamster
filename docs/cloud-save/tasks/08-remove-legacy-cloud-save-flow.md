# 08. Удаление legacy cloud-save flow

## Цель

После покрытия новыми paths удалить или заменить legacy cloud-save flow: выбор по клиентскому `LastSaveDate`, раннюю bootstrap-загрузку и отдельный skin-only upload.

## Depends on

- [02 — первая загрузка после guest link](02-first-upload-after-guest-link.md)
- [03 — атомарное восстановление аккаунта](03-restore-existing-account-atomically.md)
- [04 — очередь checkpoint upload](04-checkpoint-upload-queue.md)
- [05 — durable offline pending и reconnect](05-durable-offline-pending-and-reconnect.md)
- [06 — выбор целого снимка](06-conflict-metadata-and-whole-snapshot-selection.md)
- [07 — status и manual retry](07-sync-status-and-manual-retry.md)

## Feature links

- [Первый облачный снимок](../features/01-first-cloud-snapshot.md)
- [Восстановление существующего аккаунта](../features/02-restore-existing-account-progress.md)
- [Автоматическая синхронизация](../features/03-automatic-checkpoint-sync.md)
- [Офлайн pending и reconnect](../features/04-offline-pending-reconnect.md)
- [Безопасный выбор снимка](../features/05-conflict-safe-multi-device-sync.md)
- [Статус и ручной retry](../features/06-sync-status-manual-retry.md)

## Scope

- Удаление legacy cloud methods/call sites `GameDataManager` только после замены каждым новым path.
- Удаление выбора версии по клиентскому `LastSaveDate`.
- Устранение bootstrap cloud load, который может обогнать неблокирующий старт `AccountService`.
- Удаление отдельного skin-purchase upload после покрытия покупки общей checkpoint-очередью.
- Сохранение необходимых local save и whole-snapshot путей.

## Audit-based предполагаемые components

- Legacy cloud load/upload methods и все их call sites в `GameDataManager`.
- Bootstrap-вызов cloud load рядом с неблокирующим запуском `AccountService`.
- Отдельный upload после покупки скина.
- Account transitions `AccountService` для guest link, existing sign-in и текущего `PlayerId`.
- `PlayerData`, progress/economy/runtime storages и новые atomic apply/local-first paths.

## Минимальный алгоритм

1. Для каждого legacy call site указать заменяющий path задач 02–07 и подтвердить его acceptance.
2. Удалить ранний bootstrap cloud load; cloud-операции начинаются только после определённого account transition и текущего `PlayerId`.
3. Удалить legacy-выбор локального или облачного состояния по клиентскому `LastSaveDate`.
4. Удалить отдельный skin-only upload после подтверждения общей checkpoint-очереди для этой покупки.
5. Удалить legacy cloud methods, у которых не осталось call sites.
6. Targeted-поиском подтвердить отсутствие старых вызовов, сохранив local persistence и новые whole-snapshot paths.

## Acceptance

- Bootstrap больше не запускает cloud load до готовности account state и текущего `PlayerId`.
- Ни один активный path не выбирает снимок по клиентскому `LastSaveDate`.
- Покупка скина синхронизируется общей checkpoint-очередью, без специального cloud upload.
- Guest link, existing sign-in, checkpoints, reconnect, conflict selection и retry используют только новые whole-snapshot paths.
- В `GameDataManager` не остаются вызываемые legacy cloud methods.
- Local save/load и атомарное применение `PlayerData` продолжают работать.

## Relevant manual scenario links

- [01 — first upload success](../manual-testing/01-first-upload-success.md)
- [02 — existing cloud replaces guest](../manual-testing/02-existing-cloud-replaces-guest.md)
- [03 — failed download has no partial apply](../manual-testing/03-failed-download-no-partial-apply.md)
- [04 — checkpoint auto sync](../manual-testing/04-checkpoint-auto-sync.md)
- [05 — app close during upload](../manual-testing/05-app-close-during-upload.md)
- [06 — offline durable pending](../manual-testing/06-offline-durable-pending.md)
- [07 — reconnect uploads pending](../manual-testing/07-reconnect-uploads-pending.md)
- [08 — conflict by progression rank](../manual-testing/08-conflict-progression-rank.md)
- [09 — conflict by revision](../manual-testing/09-conflict-revision.md)
- [10 — conflict by server time](../manual-testing/10-conflict-server-time-tie.md)
- [11 — economy without field merge](../manual-testing/11-economy-no-field-merge.md)
- [12 — sync UI states and retry](../manual-testing/12-sync-ui-states-retry.md)

## Out of scope

- Широкая очистка или переразбиение `GameDataManager` и `AccountService`.
- Изменение продуктовых правил cloud save.
- Новая миграция данных, история снимков или manual conflict UI.
- Удаление local persistence, которое используют новые paths.

## KISS и сигналы рефакторинга

- Удалять только legacy-код с подтверждённой заменой и без активных call sites.
- Если удаление выявит смешение local persistence и cloud orchestration — предложить отдельный узкий refactor и дождаться одобрения.
- Если новый path зависит от legacy side effect — остановить удаление этого участка, описать зависимость и дождаться решения.
- Не выполнять соседний cleanup только потому, что он находится в том же файле.
