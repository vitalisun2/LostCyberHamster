# 05. Durable offline pending и reconnect

## Цель

Сохранить признак несинхронизированного снимка после потери сети или перезапуска и отправить новейший полный снимок после восстановления account/network readiness.

## Depends on

- [04 — очередь загрузки контрольных точек](04-checkpoint-upload-queue.md)

## Feature links

- [Офлайн-изменения и reconnect](../features/04-offline-pending-reconnect.md)

## Scope

- Durable-признак pending для конкретного `PlayerId` и версии снимка.
- Восстановление pending после запуска приложения.
- Неблокирующая отправка новейшего снимка после reconnect.
- Снятие pending только после подтверждения актуальной cloud-записи.

## Audit-based предполагаемые components

- `GameDataManager` local persistence и checkpoint upload call sites.
- `PlayerData` и progress/economy/runtime storages.
- Account readiness/success transitions `AccountService`.
- Существующая проверка доступности сети; без предположения, что bootstrap cloud load уже ждёт account resolution.

## Минимальный алгоритм

1. После local save durable-сохранить владельца и версию неподтверждённого полного снимка.
2. При offline/error оставить pending после завершения процесса.
3. На следующем запуске сначала дождаться разрешённого account state и определить текущий `PlayerId`.
4. После доступности сети выбрать новейший pending-снимок этого владельца и неблокирующе отправить его.
5. Удалить pending только если сервер подтвердил именно актуальную версию; иначе сохранить его для следующего повтора.

## Acceptance

- Офлайн-прогресс сразу сохраняется локально.
- Pending переживает закрытие и повторный запуск.
- Reconnect отправляет последний полный снимок, а не историю отдельных полей.
- Pending одного `PlayerId` никогда не загружается в другой аккаунт.
- Повторная ошибка не блокирует gameplay и не очищает pending.

## Relevant manual scenario links

- [05 — app close during upload](../manual-testing/05-app-close-during-upload.md)
- [06 — offline durable pending](../manual-testing/06-offline-durable-pending.md)
- [07 — reconnect uploads pending](../manual-testing/07-reconnect-uploads-pending.md)

## Out of scope

- Background upload при закрытом приложении.
- Хранение всех промежуточных offline-снимков.
- Multi-device conflict selection и sync UI.

## KISS и сигналы рефакторинга

- Хранить только новейший pending snapshot/version на владельца, без event log.
- Если bootstrap снова читает облако до account resolution — предложить единый readiness gate и дождаться одобрения.
- Если durable metadata прокидывается множеством отдельных параметров — предложить небольшой DTO и дождаться одобрения.
- Если reconnect/retry дублирует checkpoint upload path — предложить один переиспользуемый upload path вместо второго.
