# Account EditMode Unit Test Plan

## Цель

Исчерпывающе проверить публичные ветки production Account feature и известные failure/concurrency boundaries без реальных UGS, сети, браузера, сцен и PlayMode.

## Контракт

- `AccountState.Unknown` и `AccountLinkStatus.Unknown` — безопасные default-состояния.
- `default(AccountSnapshot)` и `default(AccountLinkResult)` нормализуют строковые значения в `string.Empty`.
- Cloud Save доступен только signed-in состояниям `Guest` и `Linked`.
- Ошибка транспорта или timeout при создании сессии дают `Offline`; configuration/SDK ошибки дают `Error`.
- Ошибка refresh/unlink сохраняет известный Player ID, но не сохраняет недоказанный `IsLinked = true`.
- `AlreadyLinked` не переключает Player ID: конфликт возвращается вызывающему UX.
- Интерактивный link использует fresh Unity Player Accounts flow и service-level single-flight.
- State-команды и refresh сериализованы: завершившаяся backend-мутация всегда публикует финальный snapshot до следующего query.
- OAuth timeout, failure и success освобождают подписки; следующий вызов может начать новый flow.
- Production account copy про сохранение прогресса не является частью unit-контракта: будущий flow свяжет identity с Cloud Save synchronization.

## Матрица

### Models

- все `AccountState × IsSignedIn` для `CanUseCloudSave`;
- `Unknown`, explicit/default snapshot, null normalization;
- default/Unknown/Success/AlreadyLinked/Failed link result;
- null normalization фабрик и невозможность считать default успешным.

### AccountService

- `EnsureSignedInAsync`: cached/anonymous, guest/linked, init/anonymous/refresh failures, Offline/Error classification, event snapshot/count/order;
- `RefreshLinkStateAsync`: signed-out/guest/linked/error, Player ID/error preservation, stale linked regression;
- `IsLinkedAsync`: signed/unsigned, guest/linked/failure, call order;
- `LinkUnityAccountAsync`: unavailable/already linked/success/empty token/gateway exception/cancel/timeout/backend failure/conflict, snapshot/events/call counts;
- service single-flight: concurrent callers share flow and result; success/failure/timeout cleanup permits retry;
- `LinkUnityAccountWithAccessTokenAsync`: invalid tokens, all result statuses, thrown auth error, refresh only after success;
- `UnlinkUnityAccountAsync`: success/error/refresh error/signed-out edge, stale linked regression;
- `StateChanged`: exact snapshot, no unexpected duplicates, subscriber exception isolation;
- `AccountServiceProvider`: override/reset isolation.

### UnityPlayerAccountGateway

- constructor boundaries;
- forced fresh sign-in instead of cached token;
- token accepted only from valid `SignedIn` event;
- `SignInFailed`, null failure, launch failure;
- timeout during launch and callback;
- early callback before `StartSignInAsync` completes;
- exact subscribe/unsubscribe/reset lifecycle;
- gateway single-flight;
- retry after success/failure/timeout.

### DevTools pure state

- typed navigation root/back stack;
- confirmation consume/cancel semantics;
- controller busy/last-result transitions where practical without Unity UI.

## Test doubles

- fake authentication gateway records call order/counts and returns configured results/exceptions;
- fake Player Account gateway uses deterministic `TaskCompletionSource` for concurrency;
- fake SDK port exposes events and subscription counts;
- fake timeout port returns controlled tasks; real sleeps are forbidden;
- static Unity services, network and browser are never called.

## Definition of Done

- tests are Unity EditMode tests with Given/When/Then names and Arrange/Act/Assert blocks;
- every production review regression has a named test;
- `Assembly-CSharp` and `Assembly-CSharp-Editor` compile without errors;
- Account EditMode suite passes in Unity Test Runner;
- no PlayMode tests are added;
- executed case totals and any unrelated runner limitations are recorded in the final report.
