Перестройка нам нужна, но legacy-механика пока должна жить рядом с новой. Вот как предлагаю двигаться:

- **Каталог уровней и типы** — выделить отдельный уровень-реестр (например, `LevelCatalogService`) вместо арифметики в `LevelManager` (`Assets/Scripts/System/LevelManagement/LevelManager.cs:14`). Храним структуры `LocationId`, `PartOfDayId`, `LevelId`, описываем имена файлов, подпапки и адреса Addressables. Сервис по фиче-флагу возвращает либо старый плоский список, либо новое дерево.
- **Ресурсы и Addressables** — перевести JSON уровней в иерархию Content/locations/<Location>/<PartOfDay>/level_XX.json, обновить метки levels и добавить метаданные для групп. Пока флаг выключен, публикуем обе схемы; LevelCatalog умеет собирать уровни из подпапок. Проверить `LevelDataProvider.LoadLevelInfo` (`Assets/Scripts/System/LevelManagement/LevelDataProvider.cs:121`) и сопутствующие ассеты.
- **Лоадеры и контроллеры** — переписать `LevelManager` и `LevelDataProvider` так, чтобы они опирались на LevelCatalog для получения адресов, списка уровней и названий (`Assets/Scripts/System/LevelManagement/LevelManager.cs:79`, `Assets/Scripts/System/LevelManagement/LevelDataProvider.cs:38`). Добавить typed-ключи вместо строк при работе с intro, декорациями и т.д.
- **UI потоки** — разбить текущий `SelectLevelScreenController` (`Assets/Scripts/UI/Screens/SelectLevelScreenController.cs:60`) на экран выбора времени суток и отдельный грид уровней (4 в ряд, динамическое число рядов). На legacy-фиче оставляем прежний `LevelItem` (`Assets/Scripts/UI/Components/LevelItem.cs:24`), на новой — вводим `DayTimeCard` и `LevelTile`, переключаемся через флаг.
- **Прогресс и сохранения** — заменить `PlayerData.LevelStars`/`OpenedLevels` на структуру с ключами вида (location, partOfDay, levelIndex) (`Assets/Scripts/GameManagement/Data/PlayerData.cs:43`). Реализовать мигратор: при включении нового режима читаем список legacy "level_XX" и раскладываем по новой схеме, при откате — продолжаем писать старый формат.
- **Фича-флаг и переключение** — добавить конфиг (например, в `UserSettings` или Addressables-параметр) и прокинуть его в менеджеры/экраны. Пока флаг выключен, удерживаем паритет поведения, покрываем оба пути smoke-тестами/интеграционными сценами и логируем выбор режима для аналитики.

## Шаги и подшаги

### 1. Каталог уровней и типы
1. ✅ **Цель:** инкапсулировать расчёт legacy-структуры в отдельный контракт.  
   **Действие:** добавлены `ILevelCatalog` и `LegacyLevelCatalog`, повторяющие текущие формулы (`Assets/Scripts/System/LevelManagement/ILevelCatalog.cs`, `Assets/Scripts/System/LevelManagement/LegacyLevelCatalog.cs`).
2. ✅ **Цель:** централизовать выбор реализации каталога без модификации вызывающего кода.  
   **Действие:** создан `LevelCatalogService`, который по умолчанию отдаёт `LegacyLevelCatalog` и позволяет позже подменить реализацию (`Assets/Scripts/System/LevelManagement/LevelCatalogService.cs`).
3. ✅ **Цель:** подготовить задел для новой модели без вмешательства в существующие типы.  
   **Действие:** добавлен `HierarchicalLevelCatalog` с внутренними DTO (`Assets/Scripts/System/LevelManagement/HierarchicalLevelCatalog.cs`).
4. ✅ **Цель:** перевести `LevelManager` на использование каталога, не ломая API.  
   **Действие:** `LevelManager` теперь опирается на `LevelCatalogService`, убраны прямые формулы и константа 4 (`Assets/Scripts/System/LevelManagement/LevelManager.cs`).
5. ✅ **Цель:** расширить контракт под новую схему, не затрагивая вызывающих.  
   **Действие:** интерфейс каталога дополнен методами для частей дня, реализованы в обеих реализациях (`Assets/Scripts/System/LevelManagement/ILevelCatalog.cs`, `Assets/Scripts/System/LevelManagement/LegacyLevelCatalog.cs`, `Assets/Scripts/System/LevelManagement/HierarchicalLevelCatalog.cs`).

### 2. Ресурсы и Addressables
1. ✅ **Цель:** понять текущую организацию уровней и меток без вмешательства.  
   **Действие:** проинвентаризированы текущие папки `Assets/Content/locations/*/levels` (по четыре JSON на локацию) и группа Addressables `Assets/AddressableAssetsData/AssetGroups/levels.asset`, где адреса `level_01…level_08` помечены лейблом `levels`.
2. ✅ **Цель:** получить параллельную иерархию уровней по времени суток, сохранив старое расположение.  
   **Действие:** создать подпапки `Morning/Afternoon/Evening/Night` в каждой локации и скопировать туда (не перемещать) соответствующие `level_XX.json`, чтобы legacy-файлы остались на месте, а новая схема получила собственный корень.
3. ✅ **Цель:** поддержать две схемы загрузки в Addressables.  
   **Действие:** настроить дополнительные или обновлённые метки/группы Addressables так, чтобы при включённом флаге каталог читал уровни из подпапок, а legacy-метки продолжали работать со старым плоским списком.
4. ✅ **Цель:** обеспечить валидность и совместимость билдов.  
   **Действие:** обновить `LevelDataProvider` и вспомогательные проверки, чтобы они удостоверялись в наличии уровней в обеих схемах и сигнализировали о расхождениях.
5. ✅ **Цель:** документировать и закрепить переходный процесс для дизайнеров и QA.  
   **Действие:** описать новую структуру и правила назначения Addressables в `refactor_plan.md` или отдельной инструкции, добавить чек-лист по включению фиче-флага и проверке доступности уровней в обоих режимах.


### 3. Лоадеры и контроллеры
1. ✅ **Цель:** зафиксировать текущий поток загрузки данных уровня и список зависимых классов.
   **Действие:** пройти `LevelManager`, `LevelDataProvider`, `LevelController`, `GameDataManager` и `SelectLevelScreenController`, задокументировать в заметках роли методов и места с арифметикой/строками.
2. ✅ **Цель:** подготовить новый набор контрактов для получения уровней по иерархическим ключам, не ломающий legacy API.
   **Действие:** расширить `LevelCatalogService` и DTO `HierarchicalLevelCatalog`, добавить фабрику построения `LocationEntry/PartOfDayEntry`, новые перегрузки в `LevelManager`, сохранив существующие методы для старого режима.
3. ✅ **Цель:** перевести `LevelDataProvider` на загрузку данных уровня через каталог, сохранив поддержку legacy строк.
   **Действие:** внедрить `LevelCatalogService` в `LevelDataProvider`, добавить перегрузки `LoadLevelInfo`/`GetAllLevelNamesAsync`, которые по фиче-флагу выбирают новую схему адресов либо текущую.
4. ✅ **Цель:** адаптировать `LevelController` и связанные менеджеры к новым идентификаторам уровней.
   **Действие:** добавить методы работы с парами `location/partOfDay` в `LevelController` и обновить `PlayNextLevel`/`SetCurrentLevel` так, чтобы они вызывали каталог, оставив legacy-методы как обёртку.
5. ✅ **Цель:** предоставить UI-контроллерам единый источник данных, независимый от схемы уровней.
   **Действие:** реализовать сервис-представление (например, `LevelSelectionModel`) поверх `LevelCatalogService`, выдающий плоский список или иерархию; текущие контроллеры до шага 4 продолжают использовать legacy API.


## Рабочие заметки

### Шаг 3 — подшаг 3.1 (Legacy поток и целевое состояние)
#### Legacy (текущая схема)
- `SelectLevelScreenController` создаёт `LevelItem` для каждого `PartOfDayEnum`, опираясь на `LevelManager.GetLevelName`, где вычисления `locationIndex * LevelsPerLocation + offset` жёстко зашиты в `LegacyLevelCatalog`. Клик по карточке вызывает `LevelController.SetCurrentLevel("level_XX")` и загрузку сцены `Game`.
- `LevelManager` хранит текущую локацию и число уровней, сравнивает фактические JSON (`LevelDataProvider.GetAllLevelNamesAsync`) с ожидаемым количеством `totalLocations * 4`, а также вычисляет номера уровней через деление/остаток.
- `LevelDataProvider` грузит `TextAsset` по адресу, совпадающему с `GameDataManager.PlayerData.CurrentLevel`, а остальные ресурсы (background, intro, bonuses и т.д.) адресует с помощью строковых префиксов (`level_XX_intro_N`, `level_XX_obstacles`).
- `LevelController` читает `GameDataManager.PlayerData.CurrentLevel`, вызывает `LevelManager.LoadLevelData`, а `PlayNextLevel` формирует новое имя `level_YY` арифметикой и записывает его обратно в `GameDataManager.PlayerData`.
- `GameDataManager.PlayerData` хранит прогресс в `LevelStars` и формирует `OpenedLevels` как словарь строк `level_XX`, поэтому доступ к прогрессу всегда идёт по плоскому идентификатору.
#### Новый подход (при включённом фиче-флаге)
Цель: заменить строковые идентификаторы на составные ключи `(LocationId, PartOfDayId, LevelId)`, сохранив legacy-путь до отключения фиче-флага.
- `LevelCatalogService` будет отдавать `HierarchicalLevelCatalog`, формируя дерево локация → часть дня → уровни, при выключенном флаге остаётся `LegacyLevelCatalog`.
- `SelectLevelScreenController` и новые UI-компоненты будут получать ViewModel из сервиса выбора уровней, содержащую иерархические ключи; текущие `LevelItem` продолжат работать, пока флаг выключен.
- `LevelController` и `LevelManager` будут уметь устанавливать текущий уровень по составному ключу и преобразовывать его в `level_XX` только при работе в legacy-режиме.
- `LevelDataProvider` станет запрашивать адреса через каталог (legacy — `level_XX`, новая схема — `<Location>/<Part>/<level_XX>`), сохраняя существующие методы для обратной совместимости.
- `GameDataManager.PlayerData` получит слой адаптации: прогресс записывается в новую структуру, но при выключенном флаге продолжаем наполнять `LevelStars` и `OpenedLevels`, чтобы старый UI и геймплей оставались рабочими.

### Шаг 3 — подшаг 3.2 (Контракты каталога)
#### Legacy (до изменений)
- `LevelCatalogService` держал только `LegacyLevelCatalog`, не умел хранить вторую реализацию и сообщать активный режим, поэтому переключение по фиче-флагу потребовало бы внешних костылей.
- `HierarchicalLevelCatalog` представлял дерево, но не имел помощников для сборки из словарей/списков и не раскрывал ключи локаций — использовать его в сервисах было неудобно.
- `LevelManager` не предоставлял перегрузок для строковых ключей частей дня, UI и контроллеры были привязаны к `PartOfDayEnum` и арифметике.
#### Новый подход (готов к включению фиче-флага)
- `LevelCatalogService` теперь хранит legacy и иерархическую реализации, предоставляет методы `ConfigureHierarchicalCatalog`, `UseHierarchicalCatalog/UseLegacyCatalog`, свойства `HasHierarchicalCatalog` и `IsHierarchical` — переключение режимов централизовано.
- `HierarchicalLevelCatalog` получил фабрику (`Factory`) и DTO `LocationDefinition`/`PartDefinition`/`LevelDefinition`, нормализует порядок уровней, умеет выдавать ключ локации и часть дня; есть `FromDictionary` для данных из Addressables.
- `LevelManager` добавил методы `GetPartOfDayKeys`, `GetLevelsForPartOfDay` и `GetLocationKey`, отдавая данные напрямую из каталога и сохраняя существующие legacy-методы без изменений.


### Шаг 3 — подшаг 3.3 (Лоадеры данных уровня)
#### Legacy (до изменений)
- `LevelDataProvider.LoadLevelInfo` загружал JSON напрямую по адресу `GameDataManager.PlayerData.CurrentLevel`, не имел fallback и возвращал только legacy-строки `level_XX`.
- `GetAllLevelNamesAsync` всегда читал Addressables-лейбл `levels`, не учитывал иерархию и не позволял внешним сервисам выбирать источник данных.
#### Новый подход (готов к включению фиче-флага)
- Добавлен публичный `LoadLevelInfo(LevelData, string)` и резолвер адресов через `LevelCatalogService`: при активном флаге берём адрес `<Location>/<Part>/level_XX`, при отсутствии — откатываемся к legacy.
- `GetAllLevelNamesAsync(bool)` переключает между legacy и day-part схемами, отдельные помощники читают нужные лейблы, нормализуют имена и мягко откатываются при пустом результате.
- Хелперы `ResolveCurrentLevelAddress` и `ExtractLegacyLevelKey` поддерживают соответствие строковых ключей `level_XX` и новых адресов, сохраняя обратную совместимость для загрузчиков и UI.


### Шаг 3 — подшаг 3.4 (Контроллер уровня и переходы)
#### Legacy (до изменений)
- `LevelController.PlayNextLevel` полагался на арифметику `level_XX`, вычисляя следующий уровень через `current + 1` и ограничение `locations * 4`, не учитывая новую иерархию или адреса `Location/Part/level_XX`.
- `SetCurrentLevel` принимал только строку `level_XX` и записывал её прямо в `PlayerData`, поэтому передача адресов подпапок ломала загрузку.
#### Новый подход (готов к включению фиче-флага)
- `SetCurrentLevel` нормализует идентификаторы и добавлена перегрузка по `(locationIndex, partOfDayKey, levelOrder)`, позволяя UI работать с иерархией; для девов есть лог-комментарии о приведении имён.
- `PlayNextLevel` теперь запрашивает следующий ключ через `LevelManager.TryGetNextLevelKey` и использует `GetTotalLevelsCount`, благодаря чему фича-флаг корректно обходит новую структуру, а legacy-поведение сохраняется через сохранённые строки `level_XX`.
- Добавлены хелперы нормализации и разрешения уровней, обеспечивающие единый путь для загрузчиков (`LevelDataProvider`) и контроллера при переключении между режимами.


### Шаг 3 — подшаг 3.5 (Модель выбора уровней)
#### Legacy (до изменений)
- `SelectLevelScreenController` и сопутствующие классы строили представление локаций на лету, напрямую обращаясь к `LevelManager` и строкам `level_XX`, поэтому любая иерархия требовала переписывать UI.
- Каталог не предоставлял агрегированного снимка данных (локации → части дня → уровни), из‑за чего каждый экран повторял арифметику и логику фильтрации.
#### Новый подход (готов к включению фиче-флага)
- Добавлен `LevelSelectionModel`, который по запросу собирает снимок уровней в legacy или иерархической форме, опираясь на `LevelCatalogService` и `LevelManager` для нормализации ключей.
- Для legacy режима модель возвращает плоские идентификаторы `level_XX`, а для day-part режима — пары `address + key`, сохраняя обратную совместимость и готовя UI к динамическому числу уровней.
- Модель объединяет метаданные локаций (`LocationInfo`), даёт список частей дня и формирует `LevelReference`, так что UI может переключаться между режимами лишь меняя флаг, не трогая арифметику.


### Шаг 2 — анализ текущих Addressables
- `Assets/Content/locations/<Location>/levels/level_XX.json` — на текущий момент по четыре файла на локацию (01_New_York и 02_Paris), уровни идут последовательно `level_01…level_08`.
- Addressables-группа `Assets/AddressableAssetsData/AssetGroups/levels.asset` содержит те же восемь уровней с адресами `level_01…level_08` и единым лейблом `levels`.
- `LevelDataProvider.LoadLevelInfo` грузит `TextAsset` по адресу, совпадающему с `GameDataManager.PlayerData.CurrentLevel` (строка вида `level_XX`).
- Созданы подпапки `Morning/Afternoon/Evening/Night` в каждой локации с копиями соответствующих `level_XX.json` и новыми `.meta` GUID.
- Создана Addressables-группа `Assets/AddressableAssetsData/AssetGroups/levels_by_daypart.asset` с адресами `<Location>/<Part>/level_XX` и метками `levels_daypart`, `levels_daypart_<Part>`, `levels_location_<Location>`.

### Step 2 Notes
- Для новой схемы уровни размещаются параллельно в подпапках `Morning/Afternoon/Evening/Night`, исходные файлы сохраняются в корне `levels/`.
- Addressables:
  - legacy: label `levels`, адреса `level_XX`.
  - day-part: группа `levels_by_daypart`, адреса `<Location>/<Part>/level_XX`, метки `levels_daypart`, `levels_daypart_<Part>`, `levels_location_<Location>`.
- При включении нового режима проверить:
  1. Все уровни отображаются в каталоге по частям дня, данные совпадают с legacy.
  2. `LevelDataProvider` без ошибок проходит валидацию (логов нет).
  3. UI переключается корректно между схемами по фиче-флагу.
  4. Сборка Addressables генерирует обе группы без предупреждений.
- Для дизайнеров: новый уровень добавляется копированием `level_XX.json` в нужную подпапку и назначением меток в Addressables (см. `levels_by_daypart` группу).
- Для QA: тестовый чек-лист включает запуск игры с legacy и day-part режимом, проверку меню выбора времени суток и загрузку уровней.
