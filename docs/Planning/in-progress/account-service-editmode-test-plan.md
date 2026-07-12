# Account Service EditMode Test Plan

## Цель

Зафиксировать набор EditMode-автотестов для `AccountService`: чистая логика методов, статусы, ошибки и edge cases без настоящего Unity backend, Cloud Save, сцен и UI.

## Что тестируем

- `AccountService`;
- `AccountSnapshot`, `AccountLinkResult`, `AccountState`, `AccountLinkStatus`;
- взаимодействие `AccountService` с fake-реализациями `IUnityAuthenticationGateway` и `IUnityPlayerAccountGateway`;
- старый `AuthenticationManager` только как compatibility facade, если нужен минимум регрессионных проверок.

## Что не тестируем в EditMode

- настоящее окно Unity Player Account;
- реальные `AuthenticationService`, `PlayerAccountService`, `UnityServices.InitializeAsync`;
- Cloud Save и восстановление игрового прогресса;
- UI Toolkit layout и клики по кнопкам;
- поведение после переустановки или на втором устройстве.

Эти проверки относятся к PlayMode, Editor/manual QA и device-тестам.

## Тестовая инфраструктура

### FakeUnityAuthenticationGateway

Минимальный fake должен уметь настраивать:

- `IsSignedIn`;
- `PlayerId`;
- результат `IsUnityAccountLinkedAsync`;
- исключение при `InitializeAsync`;
- исключение при `SignInAnonymouslyAsync`;
- исключение при `IsUnityAccountLinkedAsync`;
- результат `LinkWithUnityAsync`;
- исключение при `UnlinkUnityAsync`;
- счетчики вызовов `InitializeAsync`, `SignInAnonymouslyAsync`, `IsUnityAccountLinkedAsync`, `LinkWithUnityAsync`, `UnlinkUnityAsync`.

### FakeUnityPlayerAccountGateway

Минимальный fake должен уметь настраивать:

- access token, который вернется из `SignInAndGetAccessTokenAsync`;
- исключение при запросе access token;
- счетчик вызовов `SignInAndGetAccessTokenAsync`.

### Доступ к internal API

Сейчас constructor `AccountService(IUnityAuthenticationGateway, IUnityPlayerAccountGateway)` и gateway-интерфейсы internal. Для тестов нужно одно из решений:

- добавить `InternalsVisibleTo` для editor test assembly;
- либо сделать тесты в сборке, которая видит `Assembly-CSharp` internals;
- либо осторожно пересмотреть видимость тестируемого constructor/interface без расширения публичного runtime API.

Предпочтение: `InternalsVisibleTo`, чтобы не делать fake-oriented API публичным для игры.

## Обязательные тесты AccountService

### EnsureSignedInAsync

1. `EnsureSignedInAsync_WhenNotSignedIn_SignsInAnonymouslyAndReturnsGuest`

Проверка: вызывает `InitializeAsync`, затем `SignInAnonymouslyAsync`, затем refresh linked state. Возвращает `Guest`, `IsSignedIn = true`, `IsLinked = false`, `PlayerId` заполнен.

2. `EnsureSignedInAsync_WhenAlreadySignedIn_DoesNotSignInAnonymously`

Проверка: вызывает `InitializeAsync`, не вызывает anonymous sign-in, обновляет snapshot.

3. `EnsureSignedInAsync_WhenAlreadyLinked_ReturnsLinked`

Проверка: если fake сообщает linked account, snapshot становится `Linked`, `IsLinked = true`.

4. `EnsureSignedInAsync_WhenInitializeThrows_ReturnsOfflineSnapshot`

Проверка: метод не пробрасывает исключение, возвращает `Offline`, сохраняет `ErrorMessage`.

5. `EnsureSignedInAsync_WhenAnonymousSignInThrows_ReturnsOfflineSnapshot`

Проверка: метод не пробрасывает исключение, возвращает `Offline`, `IsSignedIn = false`.

6. `EnsureSignedInAsync_RaisesStateChangedOnSnapshotUpdate`

Проверка: событие `StateChanged` вызывается с тем же snapshot, который вернул метод.

### RefreshLinkStateAsync

1. `RefreshLinkStateAsync_WhenNotSignedIn_ReturnsUnknown`

Проверка: если auth gateway не signed-in, возвращается `Unknown`, `IsSignedIn = false`, `IsLinked = false`.

2. `RefreshLinkStateAsync_WhenSignedInAndNotLinked_ReturnsGuest`

Проверка: signed-in player без Unity link получает состояние `Guest`.

3. `RefreshLinkStateAsync_WhenSignedInAndLinked_ReturnsLinked`

Проверка: linked player получает состояние `Linked`.

4. `RefreshLinkStateAsync_WhenLinkStateCheckThrows_ReturnsErrorAndKeepsPreviousIdentity`

Проверка: метод не пробрасывает исключение, возвращает `Error`, сохраняет предыдущие `PlayerId`, `IsSignedIn`, `IsLinked` из snapshot.

5. `RefreshLinkStateAsync_RaisesStateChanged`

Проверка: событие вызывается при изменении snapshot.

### IsLinkedAsync

1. `IsLinkedAsync_WhenAlreadySignedIn_RefreshesState`

Проверка: вызывает `RefreshLinkStateAsync`-путь и возвращает актуальный `IsLinked`.

2. `IsLinkedAsync_WhenNotSignedIn_EnsuresSignInFirst`

Проверка: вызывает sign-in flow, затем возвращает linked state.

3. `IsLinkedAsync_WhenEnsureSignInFails_ReturnsFalse`

Проверка: ошибка sign-in превращается в `Offline`, метод возвращает `false`.

### LinkUnityAccountAsync

1. `LinkUnityAccountAsync_WhenOffline_ReturnsFailedAndDoesNotAskForPlayerAccountToken`

Проверка: если `EnsureSignedInAsync` вернул не signed-in snapshot, метод возвращает `Failed`, token gateway не вызывается.

2. `LinkUnityAccountAsync_WhenAlreadyLinked_ReturnsSuccessAndDoesNotAskForPlayerAccountToken`

Проверка: already linked account не запускает Unity Player Account flow повторно.

3. `LinkUnityAccountAsync_WhenGuestAndTokenReceived_LinksAccountAndRefreshesSnapshot`

Проверка: получает token, вызывает `LinkWithUnityAsync(token)`, при успехе обновляет snapshot до `Linked`.

4. `LinkUnityAccountAsync_WhenTokenGatewayThrows_ReturnsFailed`

Проверка: отмена или ошибка Unity Player Account flow не пробрасывается наружу, возвращает `Failed`.

5. `LinkUnityAccountAsync_WhenBackendReturnsAlreadyLinked_SignsInToExistingAccount`

Проверка: конфликт привязки запускает sign-in в существующий аккаунт, Player ID и snapshot обновляются до `Linked`.

6. `LinkUnityAccountAsync_WhenExistingAccountSignInFails_ReturnsFailedAndKeepsGuestSnapshot`

Проверка: ошибка восстановления возвращает `Failed`, текущий guest snapshot не помечается как linked.

7. `LinkUnityAccountAsync_WhenBackendReturnsFailed_ReturnsFailedAndKeepsGuestSnapshot`

Проверка: обычная ошибка link не ломает guest состояние.

### LinkUnityAccountWithAccessTokenAsync

1. `LinkUnityAccountWithAccessTokenAsync_WhenTokenIsNullOrEmpty_ReturnsFailed`

Проверка: `null`, empty и whitespace token не отправляются в auth gateway.

2. `LinkUnityAccountWithAccessTokenAsync_WhenLinkSucceeds_RefreshesSnapshot`

Проверка: после успешного link вызывается refresh и snapshot становится актуальным.

3. `LinkUnityAccountWithAccessTokenAsync_WhenAlreadyLinked_SignsInToExistingAccount`

Проверка: `AlreadyLinked` переключает сервис на существующий Player ID и обновляет snapshot.

4. `LinkUnityAccountWithAccessTokenAsync_WhenLinkFails_ReturnsFailed`

Проверка: failed result возвращается без исключения.

### UnlinkUnityAccountAsync

1. `UnlinkUnityAccountAsync_WhenGatewaySucceeds_RefreshesSnapshot`

Проверка: вызывает `UnlinkUnityAsync`, затем refresh.

2. `UnlinkUnityAccountAsync_WhenGatewayThrows_ReturnsErrorSnapshot`

Проверка: исключение не пробрасывается наружу, snapshot становится `Error`, error message сохраняется.

## Тесты моделей

1. `AccountSnapshot_Unknown_HasExpectedDefaults`

Проверка: `Unknown`, пустой player id, not signed-in, not linked, пустая ошибка.

2. `AccountSnapshot_CanUseCloudSave_WhenSignedInAndNotOfflineOrError`

Проверка: true для `Guest` и `Linked` signed-in snapshot, false для `Offline`, `Error`, not signed-in.

3. `AccountLinkResult_Success_HasSuccessStatusAndPlayerId`

4. `AccountLinkResult_AlreadyLinked_HasAlreadyLinkedStatusAndError`

5. `AccountLinkResult_Failed_HasFailedStatusAndError`

## Минимальные тесты AuthenticationManager facade

Эти тесты вторичны. Они нужны только чтобы не сломать старый публичный API, пока он существует.

1. `AuthenticationManager_LinkAnonymousAccountToUnityAsync_OnSuccess_RaisesSuccessEvent`

2. `AuthenticationManager_LinkAnonymousAccountToUnityAsync_OnFailure_RaisesFailedEvent`

Перед реализацией этих тестов может понадобиться сделать `AuthenticationManager` инъектируемым или оставить их на более поздний этап, потому что сейчас facade жестко использует `AccountService.Instance`.

## Приоритет реализации

1. Fake gateways.
2. `EnsureSignedInAsync`, `RefreshLinkStateAsync`, `IsLinkedAsync`.
3. `LinkUnityAccountAsync`, `LinkUnityAccountWithAccessTokenAsync`.
4. `UnlinkUnityAccountAsync`.
5. Модельные тесты.
6. Compatibility facade, если не потребует лишней архитектуры.

## Definition of Done

- все обязательные тесты AccountService реализованы как EditMode tests;
- тесты не обращаются к реальным Unity Gaming Services;
- тесты не требуют сети и авторизации Unity аккаунта;
- тесты проверяют не только return value, но и snapshot, события и счетчики вызовов fake gateway;
- негативные сценарии не пробрасывают исключения наружу, если публичный метод должен вернуть `Failed`, `Offline` или `Error`.
