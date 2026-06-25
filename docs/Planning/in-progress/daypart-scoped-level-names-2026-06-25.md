# Daypart-scoped имена уровней

## Контекст

В `Level Tilemap Editor` кнопка `Create Level` сейчас предлагает следующий `level_XX` по глобальному скану всех локаций и всех частей дня. Из-за этого в пустом `Afternoon` после `Morning/level_01..level_03` предлагается `level_04`.

Файловая и runtime-модель уже иерархическая:

- editor сохраняет уровни в `levels/{PartOfDay}/{levelKey}/{levelKey}.json`;
- список уровней в editor фильтруется по выбранному `PartOfDayEnum`;
- Addressables-адрес уровня строится как `{locationKey}/{partKey}/{levelKey}`;
- прогресс хранится по `(LocationId, PartOfDayId, LevelIndex)`;
- выбор уровня в UI уже передаёт полный address, а не короткий `levelKey`.

## Цель

Сделать имена `level_XX` независимыми внутри текущей пары `(location, daypart)`.

Пример целевого поведения:

- `01_New_York/levels/Morning/level_01..level_03` уже существуют;
- пользователь выбирает `Afternoon`;
- `Create Level` предлагает `level_01`;
- ручной ввод `level_01` разрешён в `Afternoon`, если там такого уровня ещё нет;
- повтор `level_01` в том же `Afternoon` запрещён.

## Затронутый код

- `LostCyberHamster/Assets/Editor/LevelEditor/LevelTilemapEditor.cs`
  - `HandleCreateLevelClicked()` подставляет initial value.
  - `CreateLevelWithName()` передаёт выбранный daypart при создании.
  - `RefreshLevelFilesList()` уже фильтрует список по `_selectedDaypart`.

- `LostCyberHamster/Assets/Editor/LevelEditor/LevelDataManager.cs`
  - `GetNextAvailableLevelKey()` сейчас вызывает глобальный `GenerateNextLevelKey()`.
  - `CreateNewLevel()` / `CreateNewLevelRef()` сейчас проверяют requested name глобально.
  - `BuildCanonicalLevelJsonPath()` уже сохраняет в daypart-папку.

- `LostCyberHamster/Assets/Scripts/System/LevelManagement/HierarchicalLevelCatalog.cs`
  - `TryFindLevel()` ищет сначала полный address, потом короткий `levelKey`.
  - `_levelsByKey` сейчас silently выбирает первый `level_01`, если ключ повторяется.

- `LostCyberHamster/Assets/Scripts/GameManagement/GameDataManager.cs`
  - `EnsureCurrentLevelValid()` канонизирует `CurrentLevel` в полный address или выбирает первый уровень.
  - Это покрывает legacy default `CurrentLevel = "level_01"`, если короткий ключ станет неоднозначным.

- `LostCyberHamster/Assets/Scripts/System/LevelManagement/LevelDataProvider.cs`
  - загрузка уровня и intro использует `TryFindLevel()` и fallback на переданный identifier.
  - `GetAllLevelNamesAsync()` возвращает короткие ключи; внутренних call sites нет, но при повторяющихся `level_XX` список коротких ключей теряет информацию.

## План реализации

1. Editor name scope:
   - изменить `LevelDataManager.GetNextAvailableLevelKey()` на scoped вариант с `PartOfDayEnum`;
   - считать максимум только по дескрипторам текущего `levelsDirectory` и текущего daypart;
   - учитывать только gameplay-ключи формата `level_XX`, тестовые уровни игнорировать при автоинкременте.

2. Editor uniqueness scope:
   - передавать `partOfDay` в `ResolveLevelKey()`;
   - проверять занятость requested name только внутри текущей daypart-папки текущей локации;
   - оставить нормализацию имени прежней.

3. Runtime ambiguity:
   - в `HierarchicalLevelCatalog` оставить lookup по полному address без изменений;
   - короткий `_levelsByKey` наполнять только уникальными короткими ключами;
   - если `level_01` встречается в нескольких address, `TryFindLevel("level_01")` должен вернуть `false`, а не случайный первый уровень.

4. Secondary list API:
   - добавить `LevelDataProvider.GetAllLevelAddressesAsync()` для возврата полных addresses из каталога/addressables, чтобы список не схлопывал daypart-scoped уровни;
   - оставить `GetAllLevelNamesAsync()` как совместимый alias, возвращающий те же полные addresses.

5. Проверка:
   - статически проверить call sites через `rg`;
   - проверить, что текущий `01_New_York/Morning/level_01..level_03` приводит к next `level_01` для пустого `Afternoon` по логике метода;
   - Unity recompile не запускать без отдельного явного запроса по правилу проекта.

## Риски

- Старые ручные вызовы `SetCurrentLevel("level_01")` станут невалидными, если `level_01` повторяется. Это ожидаемо: короткий ключ в новой модели неоднозначен.
- Legacy save с `CurrentLevel = "level_01"` должен быть исправлен `GameDataManager.EnsureCurrentLevelValid()` в первый полный address каталога.
- Если где-то вне найденных call sites используется `GetAllLevelNamesAsync()` как список коротких label-имён, после изменения там будут полные addresses. Внутри репозитория call sites не найдены.
