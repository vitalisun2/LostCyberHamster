# 03. Атомарное восстановление существующего аккаунта

## Цель

После явного sign-in полностью заменить гостевой прогресс B облачным снимком аккаунта A, без сохранения или объединения B.

## Depends on

- [02 — первая загрузка после guest link](02-first-upload-after-guest-link.md)

## Feature links

- [Восстановление существующего аккаунта](../features/02-restore-existing-account-progress.md)

## Scope

- Только явный вход в существующий аккаунт.
- Download полного снимка нового `PlayerId`.
- Проверка снимка до атомарной замены local/runtime состояния.
- Безусловный приоритет облака над гостем B.

## Audit-based предполагаемые components

- Success transition существующего sign-in в `AccountService`.
- Legacy cloud download/load methods и call sites `GameDataManager`.
- `PlayerData`, progress/economy/runtime storages, которые должны смениться согласованно.

## Минимальный алгоритм

1. Завершить явный sign-in и получить `PlayerId` аккаунта A.
2. Загрузить целый cloud snapshot A, не записывая B под новым владельцем.
3. Проверить целостность и принадлежность снимка A.
4. Одним переходом заменить local и runtime состояние снимком A.
5. При любой ошибке не применять частичный результат и не сохранять гостя B в A.

## Acceptance

- Cloud snapshot A полностью заменяет гостя B.
- Ни одно поле экономики или прогресса B не переносится в A.
- Runtime после успеха соответствует A, а не прежнему гостю.
- Ошибка download/validation не приводит к частичной замене.
- Снимок другого `PlayerId` не применяется.

## Relevant manual scenario links

- [02 — existing cloud replaces guest](../manual-testing/02-existing-cloud-replaces-guest.md)
- [03 — failed download has no partial apply](../manual-testing/03-failed-download-no-partial-apply.md)

## Out of scope

- Резервная копия гостя B.
- Пользовательский выбор между A и B.
- Обычные multi-device конфликты после входа.

## KISS и сигналы рефакторинга

- Реализовать один атомарный apply полного снимка, без универсального merge engine.
- Если замена runtime требует повторяющегося обновления нескольких storages — предложить единый apply boundary и дождаться одобрения.
- Если в success transition смешиваются sign-in, download, validation и UI — зафиксировать SRP-сигнал и предложить минимальное разделение до правки.
