# 04. Очередь загрузки контрольных точек

## Цель

На существующих контрольных точках сразу сохранять игру локально и неблокирующе доставлять в облако только самый актуальный полный снимок.

## Depends on

- [03 — атомарное восстановление аккаунта](03-restore-existing-account-atomically.md)

## Feature links

- [Автоматическая синхронизация](../features/03-automatic-checkpoint-sync.md)

## Scope

- Подключение существующих checkpoint save call sites.
- Local-first сохранение.
- Не более одной активной upload-попытки на текущего владельца.
- Схлопывание быстрых последовательных изменений до новейшего целого снимка.

## Audit-based предполагаемые components

- `GameDataManager` local save и его checkpoint call sites.
- Текущий отдельный cloud upload после покупки скина.
- `PlayerData` и progress/economy/runtime storages.
- Текущий `PlayerId`, полученный через account flow.

## Минимальный алгоритм

1. На checkpoint атомарно сохранить полный снимок локально.
2. Пометить этот снимок новейшим кандидатом на upload.
3. Продолжить gameplay без ожидания сети.
4. Если upload уже выполняется, заменить ожидающий кандидат более новым снимком.
5. После завершения активной попытки отправить последний актуальный кандидат тому же `PlayerId`.

## Acceptance

- Local save завершается до запуска cloud upload.
- Переходы и управление не ждут облака.
- Быстрые checkpoints не позволяют старому ответу сделать облако устаревшим.
- Специальный skin-purchase path больше не является единственной точкой upload.
- Снимок не переносится между разными `PlayerId`.

## Relevant manual scenario links

- [04 — checkpoint auto sync](../manual-testing/04-checkpoint-auto-sync.md)
- [05 — app close during upload](../manual-testing/05-app-close-during-upload.md)

## Out of scope

- Durable pending после перезапуска.
- Reconnect detection.
- Conflict selection и пользовательский status UI.

## KISS и сигналы рефакторинга

- Начать с одного latest-pending slot, не создавать универсальную очередь команд.
- Если одинаковый local-first порядок копируется в нескольких checkpoints — предложить общий helper и дождаться одобрения.
- Если управление active/pending попытками разрастается в `GameDataManager` — предложить отдельную ответственность, но не вводить её заранее.
