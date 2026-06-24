# Gameplay Level Convention Filter Plan

Дата: 2026-06-24

## Цель

Сделать так, чтобы игровой runtime видел только игровые уровни по позитивной конвенции имени `level_NN` (ровно две цифры), даже если рядом в `Assets/Content/locations/**/levels/**` лежат тестовые уровни. Тестовые уровни должны оставаться запускаемыми через editor/automation tools, которые специально работают с test levels.

EditMode/PlayMode/unit тесты в рамках этой задачи не добавляются.

## Факты по текущему устройству

1. Игровой каталог строится в `LevelCatalogRuntimeConfigurator` через `Addressables.LoadResourceLocationsAsync(Consts.LevelsDaypart, typeof(TextAsset))`.
2. Каталог затем используется как единый источник для:
   - `LevelSelectionModel` и `SelectLevelScreenController` — экран выбора уровней;
   - `LevelManager` — текущая локация, part of day, количество уровней, next level;
   - `ProgressService` / `LevelProgressSnapshot` — progress/unlock;
   - `LevelDataProvider.GetAllLevelNamesAsync()` — fallback-список имён уровней.
3. `LevelAssetsAddressableSync` сейчас навешивает labels `levels_daypart`, `levels_daypart_<Part>`, `levels_location_<Location>` на все level-related entries:
   - JSON уровня `01_New_York/Morning/level_01`;
   - JSON тестового уровня `01_New_York/Morning/test_switch_lane`;
   - intro PNG `01_New_York/Morning/level_01/intro_01`.
4. Из-за пункта 3 runtime catalog сейчас видит `test_*` как обычные уровни. Intro PNG не являются уровнями: это sprites для pre-level intro sequence, которые грузятся по прямым address `${levelAddress}/intro_01` ... `${levelAddress}/intro_10`.
5. `Tools/Test Level/Launch...` не использует игровой каталог для discovery. Он сканирует `Assets/Content/locations/**/levels/**` через `LevelDataManager.GetLevelFileDescriptors()` и отбирает level key с prefix `test`.
6. `tools/invoke_run_all_test_levels.ps1` также сканирует `test*.json` напрямую под `Assets/Content/locations`.
7. Direct launch тестового уровня проходит через `PlayerPrefs["TestLevel_Address"]`; `LoadMainMenuLoadingTask` кладёт address прямо в `GameDataManager.PlayerData.CurrentLevel`.
8. `LevelDataProvider.LoadLevelInfo()` сначала пробует найти текущий уровень в catalog, но если catalog его не знает, грузит asset напрямую по текущему address. Поэтому исключение test levels из игрового catalog не ломает прямой запуск тестов.
9. Окружение (`background`, `road`, `sky`) сейчас резолвится через catalog descriptor. Для direct address вне catalog нужен generic fallback по структуре address `<Location>/<Part>/<LevelKey>`, без знания о test levels.
10. В рабочем дереве уже есть пользовательские изменения:
    - `LostCyberHamster/Assets/AddressableAssetsData/AssetGroups/levels_by_daypart.asset`;
    - новый `LostCyberHamster/Assets/Content/locations/01_New_York/levels/Morning/level_03`.

## Конвенция

Игровой уровень:

```text
<Location>/<PartOfDay>/level_NN
```

Примеры:

```text
01_New_York/Morning/level_01
01_New_York/Morning/level_02
01_New_York/Morning/level_03
```

Важно: runtime не должен проверять `test_*` и вообще знать о существовании тестовых уровней. Он должен только принимать позитивную конвенцию gameplay level key.
В рамках текущего проекта convention строгий: `level_` + ровно две цифры, например `level_01`.

## План реализации

1. Добавить в существующий runtime-класс `HierarchicalLevelCatalog` общие helpers:
   - `TryParseLevelAddress(address, out locationKey, out partKey, out levelKey)` — generic parsing address shape `<Location>/<Part>/<LevelKey>`.
   - `IsGameplayLevelKey(levelKey)` — true только для `level_` + ровно две цифры.
   - `IsGameplayLevelAddress(address)` — true только если address имеет shape level address и level key проходит gameplay convention.
2. Обновить `LevelCatalogRuntimeConfigurator.BuildLayout()`:
   - парсить entries через `TryParseLevelAddress`;
   - добавлять в catalog только `IsGameplayLevelKey(levelKey)`;
   - silently игнорировать non-gameplay entries, включая test JSON и intro PNG.
3. Обновить `LevelDataProvider.GetHierarchicalLevelNamesAsync()` тем же `IsGameplayLevelAddress()` fallback-фильтром.
4. Обновить `EnvironmentKeyResolver`:
   - основной путь оставить через catalog;
   - если catalog не знает текущий address, разобрать его через generic `TryParseLevelAddress` и использовать `<Location>/<Part>` из address;
   - не добавлять проверок `test_*`.
5. Обновить `LevelAssetsAddressableSync`:
   - gameplay labels `levels_daypart`, `levels_daypart_<Part>`, `levels_location_<Location>` ставить только на gameplay JSON entries;
   - для non-gameplay entries и intro PNG оставлять addressable entry/address, но снимать stale gameplay labels, если они уже были навешаны раньше;
   - intro PNG не удалять из Addressables и не менять схему direct load by address.
   - direct address load для тестов и intro load by address остаются рабочими.
6. Не менять `TestLevelLauncher` и PowerShell discovery, кроме если чтение покажет прямую необходимость. Сейчас они уже являются владельцами test-level discovery.
7. После правок выполнить лёгкую проверку:
   - grep/static inspection изменённых файлов;
   - скриптом/PowerShell проверить, что текущий `levels_by_daypart.asset` после sync-логики должен оставлять в gameplay catalog только `level_01`, `level_02`, `level_03`;
   - compile/recompile через Unity automation не запускать без отдельного явного запроса.

## Ожидаемый результат

Игровое меню, progress/unlock и next-level cycle работают только с `level_NN`. Тестовые уровни остаются в той же папке и продолжают запускаться через test-level tools по прямым address, но игровой каталог их не видит.
