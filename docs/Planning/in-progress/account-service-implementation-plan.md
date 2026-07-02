# Account Service Implementation Plan

## Цель

Выделить минимальный account layer: гостевой вход, состояние аккаунта и привязка Unity Player Account без прямого использования Unity Authentication API из UI и bootstrap-кода.

## Архитектурная схема

```text
Bootstrap / UI
  -> AccountService
       -> UnityAuthenticationGateway
       -> UnityPlayerAccountGateway

GameDataManager
  -> позже: PlayerDataService
       -> PlayerPrefsPlayerDataStore
       -> CloudSavePlayerDataStore
```

## Текущий шаг: AccountService

### 1. Account model
**Файлы:** `Assets/Scripts/Account/AccountState.cs`, `AccountSnapshot.cs`, `AccountLinkStatus.cs`, `AccountLinkResult.cs`

**Что меняется:** добавляются простые модели состояния аккаунта и результата привязки.

**Суть изменения:** account flow получает явный контракт: `Guest`, `Linked`, `Offline`, `Error`, без знания о монетах, уровнях или скинах.

### 2. UGS adapters
**Файлы:** `Assets/Scripts/Account/UnityAuthenticationGateway.cs`, `UnityPlayerAccountGateway.cs`

**Что меняется:** Unity Authentication и Unity Player Account API закрываются тонкими gateway-классами.

**Суть изменения:** UI и gameplay-код больше не зависят напрямую от `AuthenticationService` и `PlayerAccountService`.

### 3. AccountService
**Файл:** `Assets/Scripts/Account/AccountService.cs`

**Что меняется:** добавляется сервис с методами `EnsureSignedInAsync`, `IsLinkedAsync`, `LinkUnityAccountAsync`.

**Суть изменения:** первый запуск теперь должен создавать guest identity, а привязка аккаунта идет через один account-сервис.

### 4. Bootstrap integration
**Файл:** `Assets/Scripts/Entry Points/BootstrapLoadingTasks/AuthenticateUserLoadingTask.cs`

**Что меняется:** bootstrap вызывает `AccountService.Instance.EnsureSignedInAsync()`.

**Суть изменения:** задача аутентификации больше не делает ручной `UnityServices.InitializeAsync()` и не зависит от старого `AuthenticationManager`.

### 5. Save-progress UI integration
**Файлы:** `Assets/Scripts/Entry Points/MenuEntryPoint.cs`, `Assets/Scripts/UI/Modals/SigninModalController.cs`, `Assets/Scripts/UI/Modals/SettingsModalController.cs`, `Assets/Content/ui/uxml/SigninModal.uxml`, `Assets/Content/ui/uxml/SettingsModal.uxml`

**Что меняется:** меню больше не показывает sign-in автоматически; настройки получают строку `Аккаунт` со статусом и кнопкой сохранения прогресса; модалка объясняет ценность привязки и дает выбор `Сохранить` / `Позже`.

**Суть изменения:** первый вход остается тихим guest-входом, а игрок сам запускает привязку аккаунта из понятной UI-точки. UI ждёт результат привязки напрямую и не подписывается на события старого менеджера.

### 6. Compatibility facade
**Файл:** `Assets/Scripts/Auth/AuthenticationManager.cs`

**Что меняется:** старый manager превращается в thin facade поверх `AccountService`.

**Суть изменения:** существующий публичный API не удаляется резко, но новая ответственность живёт в `Account/`.

## Следующий шаг: PlayerDataService

AccountService не должен сохранять `PlayerData`. Следующий слой должен быть отдельным:

- `PlayerDataService` - главный сервис загрузки, локального сохранения и облачной синхронизации `PlayerData`;
- `IPlayerDataStore` - контракт хранилища;
- `PlayerPrefsPlayerDataStore` - локальное encrypted хранилище;
- `CloudSavePlayerDataStore` - Cloud Save хранилище;
- `GameDataManager` - временный фасад для старого игрового кода.

## Не входит в этот шаг

- Leaderboards.
- Cloud Code и server-authoritative экономика.
- Публичный профиль игрока.
- Сложный UI конфликтов.
- Полная миграция всех вызовов `GameDataManager.SaveData()`.
- Финальный flow восстановления существующего linked account после переустановки; для этого нужен следующий шаг с `PlayerDataService` и UX-решением по конфликту локального guest-progress.
