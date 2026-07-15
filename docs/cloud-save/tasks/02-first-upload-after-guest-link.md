# 02. Первая загрузка после привязки гостя

## Цель

После успешной привязки гостя неблокирующе отправить его полный локальный снимок в облако текущего `PlayerId`.

## Depends on

- [01 — контракт полного снимка](01-cloud-snapshot-contract-and-local-persistence.md)

## Feature links

- [Первый облачный снимок](../features/01-first-cloud-snapshot.md)

## Scope

- Только успешная привязка текущего гостя, не вход в существующий аккаунт.
- Сохранение текущего снимка локально перед отправкой.
- Первая атомарная cloud upload для неизменившегося `PlayerId`.
- Неблокирующий успех или ошибка без потери локального прогресса.

## Audit-based предполагаемые components

- Success transition привязки в `AccountService`.
- `GameDataManager` local save и существующие legacy cloud upload methods/call sites.
- `PlayerData` и текущие progress/economy/runtime storages.

## Минимальный алгоритм

1. Получить подтверждённый успех guest link и актуальный `PlayerId`.
2. Сохранить полный снимок локально.
3. Неблокирующе отправить этот же снимок в облако данного `PlayerId`.
4. Считать первый cloud snapshot созданным только после подтверждения сервера.
5. При ошибке оставить локальный снимок неизменным и доступным для повтора.

## Acceptance

- Upload не начинается до успешного guest link.
- `PlayerId` до и после link не меняется.
- В облако уходит весь актуальный гостевой снимок.
- Ошибка не блокирует gameplay и не удаляет локальное состояние.
- Sign-in существующего аккаунта не попадает в этот path.

## Relevant manual scenario links

- [01 — first upload success](../manual-testing/01-first-upload-success.md)
- [05 — app close during upload](../manual-testing/05-app-close-during-upload.md)

## Out of scope

- Durable offline pending и reconnect.
- Восстановление существующего аккаунта.
- Conflict selection и sync UI.

## KISS и сигналы рефакторинга

- Подключить upload непосредственно к существующему success transition, без event framework наперёд.
- Если link-success начинает выполнять identity, persistence и cloud orchestration в одном методе — предложить выделение одной ответственности и дождаться одобрения.
- Если upload повторяет существующую сериализацию `GameDataManager` — предложить общий путь вместо второго формата.
