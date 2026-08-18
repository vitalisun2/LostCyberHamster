# План реализации

## Контракт

- `Development Points` — отдельная meta-валюта. Один Player Level даёт один point.
- Обычные звёзды прохождения уровней остаются в `LevelProgressSnapshot` и не участвуют в развитии.
- Один point открывает один skin или одну super ability.
- Skin unlock, покупка за ресурс и экипировка — три отдельные стадии.
- Ability unlock и active selection — две отдельные стадии.
- XP balance и награды остаются текущими.

## Текущее состояние

- `PlayerExperienceService.GrantExperience` считает несколько level-up за одно начисление, сохраняет XP remainder и меняет `PlayerData.PlayerLevel`.
- `PlayerData` сохраняется целиком через `GameDataManager`, `PlayerProgressCommitter` и cloud snapshot.
- `SkinManager` владеет catalog, покупкой и экипировкой. `PurchasedSkinIds` и `AppliedSkinId` уже persisted.
- `SuperAttackService` владеет catalog и active selection. Unlock сейчас вычисляется из `RequiredPlayerLevel`.
- `UiWinModalMechanics` и `LevelUpModalController` показывают ability, автоматически доступную по новому level.
- `SuperAttacksScreen` показывает XP и ability catalog. `CharacterScreen` показывает skin carousel.
- `HomeScreenController` ведёт `XP` в `SuperAttacksScreen`, hamster button — в `CharacterScreen`.

## Владельцы данных и сервисов

### PlayerData

Файл: `LostCyberHamster/Assets/Scripts/GameManagement/PlayerProgress/PlayerData.cs`.

Добавить:

- `DevelopmentProgressVersion`;
- `DevelopmentPoints`;
- `UnlockedSkinIds`;
- `UnlockedSuperAttackIds`.

`PurchasedSkinIds`, `AppliedSkinId` и `ActiveSuperAttackId` сохраняют текущий смысл.

### CharacterDevelopmentService

Новый файл: `LostCyberHamster/Assets/Scripts/SharedCore/Meta/CharacterDevelopment/CharacterDevelopmentService.cs`.

Ответственность:

- читать balance и unlock collections из `PlayerData`;
- проверять skin/ability ID через production catalogs;
- `IsSkinUnlocked` и `IsSuperAttackUnlocked`;
- `TryUnlockSkin` и `TryUnlockSuperAttack`;
- атомарно списывать один point, добавлять ID и делать `PlayerProgressCommitter.Commit`;
- default skin ID `0` считать открытым.

`CheckpointReason` получает отдельную причину `CharacterDevelopmentUnlocked`.

### XP

Файл: `PlayerExperienceService.cs`, метод `GrantExperience`.

- Добавить `playerLevelsGained` к `DevelopmentPoints` в том же расчёте, где меняются XP и Player Level.
- Один grant с несколькими level-up выдаёт столько же points.
- Обычные level stars остаются только источником части XP.

### Skins

Файлы: `SkinManager.cs`, `Skin.cs`.

- `CanPurchaseSkin` и `PurchaseSkin` требуют development unlock.
- Покупка продолжает использовать `ResourceManager.SpendResource`, quest events и `SkinPurchased` checkpoint.
- `PutOnSkin` продолжает требовать purchase ownership.
- UI читает unlock через `CharacterDevelopmentService`, purchase/equip — через `SkinManager`.

### Super abilities

Файлы: `SuperAttackService.cs`, `SuperAttackData.cs`, `super_attacks.json`.

- `IsUnlocked(int id)` читает persisted unlock state.
- `TrySelect` разрешает только unlocked ID и сохраняет active selection текущим checkpoint.
- Удалить level-based unlock и `TryGetFirstUnlockedBetweenLevels`.
- Заменить `RequiredPlayerLevel` на `DescriptionLocalizationKey` для equipment preview.
- Runtime factory и `InitCharacterLoadingTask` продолжают читать `ActiveSuperAttackId`.

## Persistence и migration

Файл: `PlayerDataValidator.cs`.

Текущая schema: version `1`.

Для save без development schema:

- `UnlockedSkinIds` получает default skin и все уже купленные skins;
- валидная active ability получает unlock, чтобы текущий loadout сохранился;
- исторические level-up дают `max(0, PlayerLevel - 1)` earned points;
- сохранённая active ability считается одним уже потраченным point;
- обычные level stars не читаются и не конвертируются;
- migration ставит version `1`, затем обычная validation проверяет результат.

Validation version `1`:

- non-negative `DevelopmentPoints`;
- collections существуют, без отрицательных и duplicate ID;
- IDs присутствуют в production catalogs;
- default skin открыт;
- купленный и надетый skin открыт;
- active ability открыта.

Repair нормализует null collections, default skin, duplicates и legacy schema. Primary, backup и cloud используют тот же `PlayerData` JSON.

## UI и navigation

### Экран развития

Переименовать production route `SuperAttacksScreen` в `CharacterDevelopmentScreen`, сохранив asset GUID:

- `Scripts/UI/Screens/CharacterDevelopmentScreenController.cs`;
- `Content/ui/uxml/CharacterDevelopmentScreen.uxml`;
- `Content/ui/styles/screens/CharacterDevelopmentScreen.uss`;
- `ScreenEnum.CharacterDevelopmentScreen`;
- address `CharacterDevelopmentScreen` в `AddressableAssetsData/AssetGroups/UI.asset`;
- registration в `MenuEntryPoint`.

Композиция по референсу:

- header и back;
- слева XP, Player Level, Development Points и переход в equipment;
- справа две горизонтальные линии карточек: skins и super abilities;
- unlocked card показывает статус;
- locked card при доступном point вызывает соответствующий `TryUnlock...`;
- после unlock экран обновляет balance и обе линии;
- skin preview берётся из `SkinManager`, ability icons — через `AddressableLease`.

### Экран экипировки

Перестроить:

- `CharacterScreenController.cs`;
- `CharacterScreen.uxml`;
- новый `styles/screens/CharacterScreen.uss`.

Композиция по референсу:

- tabs `Skins` и `Abilities`;
- слева выбранный preview, name, description/status и action;
- справа grid доступных карточек.

Skin flow:

- карточка выбирает preview;
- unlocked, unpurchased skin показывает текущую production price/action;
- purchased skin показывает equip;
- equipped skin показывает статус без action;
- action вызывает только `SkinManager.PurchaseSkin` или `PutOnSkin`.

Ability flow:

- grid показывает unlocked abilities;
- click сразу вызывает `SuperAttackService.TrySelect`;
- preview показывает icon, name, localized description и active status.

### Home

Файлы: `HomeScreenController.cs`, `MainButtonGroup.uxml`.

- XP button получает имя `btn_development` и ведёт в `CharacterDevelopmentScreen`.
- hamster/skins button продолжает вести в `CharacterScreen`, теперь equipment.

### Level-up modal

Файлы: `UiWinModalMechanics.cs`, `LevelUpModalController.cs`, `LevelUpModal.uxml`, `LevelUpModal.uss`.

- Показать переход level и количество полученных Development Points.
- Убрать ability icon и auto-unlock message.
- Modal появляется при фактическом gameplay level-up и не меняет unlock state.

### Localization

Файлы: `lang.ru.json`, `lang.en.json`, `super_attacks.json`.

Добавить тексты development screen, equipment tabs/actions/statuses, ability descriptions и новую level-up reward message. Удалить level requirement strings из текущего flow.

## Зависимые production flows

- `StoryQuestGenerator`: ability candidates фильтровать по persisted unlock.
- `TutorialSkinLessonController/View`: выбрать target skin card вместо carousel next; tutorial sandbox открывает target skin перед purchase lesson.
- `TutorialSession.ApplyDefaultTrainingState`: заполнить development unlock target без изменения сохранённого snapshot игрока.
- `QuestTestRunner` и `SkateboardTestingRunner`: заменить level-based ability availability на development unlock через DEV helper.
- `PlayerDataValidator` и существующие serialization/validator fixtures: обновить только для нового обязательного persisted state.

## Проверка

Ручная проверка diff:

- каждый level-up добавляет exact number of Development Points;
- points сохраняются и накапливаются;
- unlock списывает один point один раз;
- level stars не меняются;
- skin unlock не покупает и не надевает skin;
- ability unlock не делает её active;
- equipment вызывает существующие purchase/equip/select flows;
- legacy save сохраняет purchased/applied skin и active ability;
- tutorial skin flow использует новый equipment UI;
- UI icon leases освобождаются при уходе с экрана.

Финальный C# gate по правилам проекта:

1. `regenerate_project_files` через Automation Bridge.
2. `dotnet build LostCyberHamster/Assembly-CSharp.csproj --no-restore`.

## Риски

- `JsonUtility` даёт `0/null` для отсутствующих полей. Versioned migration выполняется до strict development validation.
- Catalogs должны быть готовы до migration. Bootstrap уже загружает skins и super abilities перед `GameDataManager.LoadDataAsync`.
- Tutorial зависит от конкретных UI names. Новые устойчивые names обновляются вместе с view resolver.
- Addressables route должен сохранить GUID UXML и получить новое semantic address.
- Async ability icon loads должны отменяться и освобождаться при screen transition.
