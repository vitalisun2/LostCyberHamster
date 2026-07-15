# 07. Статус синхронизации и ручной retry

## Цель

Показывать честный статус `Saved`, `Syncing`, `Offline` или `Error` и позволить пользователю неблокирующе повторить отправку актуального целого снимка.

## Depends on

- [06 — выбор целого снимка](06-conflict-metadata-and-whole-snapshot-selection.md)

## Feature links

- [Статус синхронизации и ручной повтор](../features/06-sync-status-manual-retry.md)

## Scope

- Один наблюдаемый sync status для текущего UGS `PlayerId`.
- `Saved` только после подтверждения актуального снимка облаком.
- `Syncing` во время активной неблокирующей операции.
- `Offline` для pending-снимка без доступного соединения; `Error` после иной ошибки.
- Ручной retry через существующий upload/reconcile path без параллельных дублей.

## Audit-based предполагаемые components

- Legacy cloud methods/call sites `GameDataManager` и новые active/pending состояния синхронизации.
- Account transitions `AccountService`, определяющие текущий `PlayerId` и смену аккаунта.
- `PlayerData`, progress/economy/runtime storages как источник актуального whole snapshot.
- Существующий UI account/settings, в котором будет виден status и действие retry.

## Минимальный алгоритм

1. Показать `Saved`, только если актуальный локальный снимок подтверждён облаком и нет active/pending работы.
2. При запуске upload, download или conflict resolution показать `Syncing`, не блокируя gameplay.
3. Если актуальный снимок остаётся pending без сети, показать `Offline`.
4. Если операция завершилась несетевой ошибкой, сохранить pending и показать `Error`.
5. Retry из `Offline` или `Error` запускает тот же неблокирующий sync path для самого нового целого снимка.
6. Повторные нажатия не создают конкурирующие попытки; pending снимается только после подтверждённого результата.

## Acceptance

- В каждый момент UI показывает ровно одно из четырёх состояний.
- `Saved` никогда не показывается для неподтверждённого актуального снимка.
- Gameplay остаётся доступным во время `Syncing` и retry.
- Сетевая недоступность приводит к `Offline`, иная ошибка — к `Error`; локальный pending сохраняется.
- Успешный retry целого актуального снимка переводит состояние в `Saved`.
- Старый ответ или повторный клик не применяет устаревший снимок поверх нового.

## Relevant manual scenario links

- [06 — offline durable pending](../manual-testing/06-offline-durable-pending.md)
- [07 — reconnect uploads pending](../manual-testing/07-reconnect-uploads-pending.md)
- [12 — sync UI states and retry](../manual-testing/12-sync-ui-states-retry.md)

## Out of scope

- Визуальный редизайн account/settings UI.
- История операций и подробный пользовательский журнал.
- Ручной выбор локального или облачного снимка.
- Фоновая синхронизация при закрытом приложении.

## KISS и сигналы рефакторинга

- Вычислять четыре состояния из уже существующих active/pending/result фактов, без отдельной state-machine framework.
- Если UI начинает самостоятельно решать, синхронизирован ли снимок — предложить один read-only источник статуса и дождаться одобрения.
- Если retry дублирует upload/reconcile алгоритм — предложить один общий entry point и дождаться одобрения.
- Если account UI смешивает отображение, сетевые вызовы и применение `PlayerData` — предложить минимальное разделение ответственности и дождаться одобрения.
