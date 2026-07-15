# 06. Метаданные конфликта и выбор целого снимка

## Цель

Для обычной синхронизации одного UGS `PlayerId` детерминированно выбирать один целый снимок по порядку progression rank → revision → server time, без полевого объединения данных.

## Depends on

- [05 — durable offline pending и reconnect](05-durable-offline-pending-and-reconnect.md)

## Feature links

- [Безопасный выбор снимка](../features/05-conflict-safe-multi-device-sync.md)

## Scope

- Метаданные актуальности целого снимка: progression rank, revision и server time.
- Сравнение локального и облачного снимков только одного `PlayerId` при обычной синхронизации.
- Выбор и целостное применение одного победителя.
- Отдельный path явного входа в существующий аккаунт: облако побеждает без сравнения с гостем.
- Полный запрет field-wise merge прогресса и экономики.

## Audit-based предполагаемые components

- Legacy cloud methods/call sites `GameDataManager`, включая выбор по клиентскому `LastSaveDate`.
- Account transitions `AccountService`, различающие обычную same-account sync и явный sign-in существующего аккаунта.
- `PlayerData`, progress/economy/runtime storages и атомарный apply целого снимка.
- Whole-snapshot contract, pending state и reconnect flow предыдущих задач.

## Минимальный алгоритм

1. Различить обычную same-account sync и явный existing-account sign-in.
2. Для явного sign-in применить правило задачи 03: целый cloud snapshot безусловно заменяет гостя B.
3. Для обычной sync проверить целостность и одинаковый `PlayerId` локального и облачного снимков.
4. Сначала сравнить progression rank, при равенстве — revision, затем — server time.
5. Выбрать один целый снимок и целиком применить или записать его на отстающую сторону.
6. Не копировать отдельные поля из проигравшего снимка и не использовать клиентский `LastSaveDate` как критерий.

## Acceptance

- Более высокий progression rank побеждает любые revision и server time проигравшего снимка.
- При равном rank побеждает большая revision; при равных rank и revision — более позднее server time.
- Результат состоит только из одного whole snapshot; экономика не объединяется по полям.
- Снимки разных `PlayerId` не сравниваются и не применяются друг к другу.
- Explicit existing sign-in всегда применяет cloud snapshot и отбрасывает гостя B отдельно от conflict comparator.
- Клиентский `LastSaveDate` не участвует в новом выборе.

## Relevant manual scenario links

- [02 — existing cloud replaces guest](../manual-testing/02-existing-cloud-replaces-guest.md)
- [08 — conflict by progression rank](../manual-testing/08-conflict-progression-rank.md)
- [09 — conflict by revision](../manual-testing/09-conflict-revision.md)
- [10 — conflict by server time](../manual-testing/10-conflict-server-time-tie.md)
- [11 — economy without field merge](../manual-testing/11-economy-no-field-merge.md)

## Out of scope

- Ручной выбор версии и история снимков.
- Merge engine для отдельных полей.
- Пользовательский sync status и retry.
- Удаление всех legacy cloud paths до задачи 08.

## KISS и сигналы рефакторинга

- Начать с одного последовательного comparator, без универсального rules engine.
- Если одинаковое сравнение появляется в нескольких call sites — предложить один comparator и дождаться одобрения.
- Если выбор победителя начинает напрямую менять несколько runtime storages — предложить переиспользовать единый atomic apply boundary и дождаться одобрения.
- Если различение sign-in и same-account sync размазывается между слоями — предложить одну явную границу перехода и дождаться одобрения.
