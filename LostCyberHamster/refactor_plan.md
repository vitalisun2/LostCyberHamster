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



### 5. Прогресс и сохранения
1. ✅ **Цель:** собрать карту использования legacy-прогресса и сохранений.  
   **Действие:** зафиксировать все обращения к CurrentLevel/LevelStars/OpenedLevels и описать текущее состояние данных в заметках (refactor_plan.md:128).
2. ✅ **Цель:** спроектировать новый контракт прогресса с типизированным ключом.  
   **Действие:** определить структуры (LocationId, PartOfDayId, LevelIndex) и договориться о DTO/интерфейсах, чтобы каталог и сохранения работали единообразно.
3. ✅ **Цель:** обновить PlayerData и сериализацию под новую схему.  
   **Действие:** добавить новое поле прогресса, оставить адаптер к legacy-формату и описать целевой JSON для feature-флага.
4. ✅ **Цель:** реализовать миграцию и обратную совместимость в загрузке/сохранении.  
   **Действие:** преобразовывать старые сейвы при включении флага и формировать legacy-данные при выключенном режиме (GameDataManager.LoadDataAsync/SaveData).
5. ✅ **Цель:** перевести потребителей на новое API прогресса.  
   **Действие:** обновить LevelManager, UI и связанные сервисы, чтобы они читали звёзды/доступность через новый слой с учётом фиче-флага.
6. ✅ **Цель:** провалидировать сохранения и покрыть миграцию тестами.  
   **Действие:** добавить автотесты/чек-листы для локальных и облачных сейвов, задокументировать результаты для QA.


### 6. Фича-флаг и переключение
1. ✅ **Цель:** зафиксировать текущие точки включения режимов и зависимости от каталога.  
   **Действие:** пройти инициализацию (LevelCatalogService, загрузки/бутстрап), описать порядок подключения каталога и места выбора режима.
2. ✅ **Цель:** добавить конфигурацию фиче-флага и хранение состояния.  
   **Действие:** расширить настройки (SettingsData) и сервис чтения, определить дефолтный режим и способ изменения.
3. ✅ **Цель:** связать флаг с системами уровня/прогресса/UI.  
   **Действие:** при старте переключать каталог, инициировать мигратор, уведомлять UI и сервисы; предусмотреть hot-switch для девов.
4. ✅ **Цель:** внедрить аналитику и логи для контроля режима.  
   **Действие:** добавить события/логи при выборе режима, предусмотреть отчёт в консоли/аналитике.
5. ⏳ **Цель:** подготовить проверки и документацию для QA и релиза.  
   **Действие:** составить чек-лист переключения, описать процедуры smoke-теста в обоих режимах и обновить инструкцию для команды.

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

### Шаг 5 — подшаг 5.1 (Анализ legacy-прогресса)
#### Legacy
- PlayerData.CurrentLevel хранит строку вида level_XX; её читают загрузчики и контроллеры (Assets/Scripts/System/LevelManagement/LevelDataProvider.cs:44, Assets/Scripts/System/LevelManagement/LevelManager.cs:368, Assets/Scripts/System/LevelManagement/LevelController.cs:167).
- Прогресс хранится в PlayerData.LevelStars как список, а открытые уровни вычисляются на лету через OpenedLevels (Assets/Scripts/GameManagement/Data/PlayerData.cs:50). UI и менеджеры опираются на словарь (Assets/Scripts/System/LevelManagement/LevelManager.cs:404, Assets/Scripts/UI/Components/LevelItem.cs:123).
- Начисление звёзд и анлок происходят в LevelManager.OnLevelComplited/OpenNextLevel, где жёстко используется арифметика по LevelsPerLocation и порядковым номерам (Assets/Scripts/System/LevelManagement/LevelManager.cs:462, Assets/Scripts/System/LevelManagement/LevelManager.cs:495).
- StarsToOpenNewLocation, GetLocationIndex, GetCurrentPartOfDay и переход на следующий уровень полагаются на деление/остаток от level_XX (Assets/Scripts/System/LevelManagement/LevelManager.cs:360, Assets/Scripts/System/LevelManagement/LevelManager.cs:444, Assets/Scripts/System/LevelManagement/LevelManager.cs:212).
- Сохранения сериализуют PlayerData целиком: локально через GameDataManager.SaveData, в облако через TrySaveToCloud без дополнительной проверки (Assets/Scripts/GameManagement/GameDataManager.cs:99, Assets/Scripts/GameManagement/GameDataManager.cs:120).

#### Новый подход
- Ввести типизированный ключ прогресса (локация, часть дня, индекс) и хранить статусы уровней в структуре, совместимой с HierarchicalLevelCatalog (Assets/Scripts/System/LevelManagement/HierarchicalLevelCatalog.cs).
- Обновить контракты LevelManager/UI так, чтобы они брали открытые уровни и звёзды из нового слоя вместо словаря level_XX.
- Подготовить миграцию: при чтении legacy-сейва строить иерархический прогресс, при выключенном флаге продолжать экспортировать линейные level_XX для обратной совместимости.

#### Наблюдения
- LevelDataProvider.ResolveCurrentLevelAddress уже умеет искать адреса в каталоге; можно переиспользовать эту точку для маппинга нового ключа без прямой зависимости от строк.
- Логика открытия локаций привязана к сумме OpenedLevels.Values, понадобятся хелперы, считающие прогресс по новой структуре.
- Любые правки должны учитывать, что OpenNextLevel вызывается из событий GameEventsManager, поэтому миграция данных должна быть idempotent на момент вызова.
### Шаг 5 — подшаг 5.2 (Контракт прогресса)
#### Реализация
- Добавлены типы LevelProgressKey/LevelProgressEntry/LevelProgressSnapshot для хранения прогресса (Assets/Scripts/GameManagement/Data/LevelProgressModels.cs).
- Реализованы фабрики CreateFromCatalog/CreateLegacySkeleton, позволяющие заполнять прогресс из иерархического каталога или legacy-схемы.
- Подготовлены адаптеры LevelProgressKeyAdapters для преобразования level_XX в типизированные ключи и обратно (Assets/Scripts/GameManagement/Data/LevelProgressKeyAdapters.cs).
- Нормализован contract: уровни внутри части дня индексируются с нуля, MaxStars ограничен тремя, Unlock/ApplyStars возвращают новые экземпляры.
### Шаг 5 — подшаг 5.3 (PlayerData и сериализация)
#### Реализация
- PlayerData хранит legacy LevelStars и новый снимок LevelProgressSnapshot; перед сериализацией звёзды синхронизируются (Assets/Scripts/GameManagement/Data/PlayerData.cs).
- Добавлен список сериализуемых DTO _serializedProgress для JsonUtility, а Progress восстанавливает снимок лениво.
- Сохранили старый формат OpenedLevels, чтобы потребители до миграции получали ключи level_XX.

### Шаг 5 — подшаг 5.4 (Миграция и обратная совместимость)
#### Реализация
- PlayerProgressMigration.Initialize формирует снимок прогресса по активному каталогу и накладывает данные LevelStars (Assets/Scripts/GameManagement/Data/PlayerProgressMigration.cs).
- GameDataManager.LoadDataAsync вызывает мигратор после чтения сейва и пересохраняет данные, чтобы зафиксировать _serializedProgress (Assets/Scripts/GameManagement/GameDataManager.cs).
- При отсутствии иерархии мигратор оставляет fallback и не ломает legacy-путь (используется ленивое восстановление в PlayerData).

### Шаг 5 — подшаг 5.5 (Потребители нового прогресса)
#### Реализация
- LevelManager теперь ориентируется на LevelProgressSnapshot: открытость и звёзды уровней берутся из снимка, добавлены адаптеры TryGetProgressKey и миграция к иерархии (Assets/Scripts/System/LevelManagement/LevelManager.cs).
- Обновлены обработчики завершения уровня: при иерархии прогресс обновляется через LevelProgressEntry.ApplyStars, разблокировка следующего уровня работает через UnlockNextLevelHierarchical.
- Legacy-путь сохранён: при выключенной иерархии поведение прежнее (модифицированный HandleLegacyLevelCompletion).

### Шаг 5 — подшаг 5.6 (Валидация сохранений)
#### План тестирования
- Юнит-чек: сериализация PlayerData с legacy LevelStars → ToJson → FromJson возвращает те же звёзды и создаёт снимок (покрывается будущими тестами).
- Smoke для миграции:
  1. Запустить игру с legacy-режимом, пройти/открыть несколько уровней, убедиться, что PlayerData.ToJson() содержит _serializedProgress.
  2. Включить day-part режим, вызвать PlayerProgressMigration.Initialize, проверить LevelManager.IsLevelOpen/GetLevelStars.
  3. Пройти ещё один уровень, убедиться, что LevelProgressSnapshot обновился и LevelStars синхронизированы при SaveData.
  4. Сохранить в облако, перезапустить клиент: загрузка выбирает новое представление и пересохраняет данные без потерь.
- QA чек-лист: переключение фиче-флага, сравнение PlayerPrefs до/после миграции, smoke UI в обоих режимах.

### Шаг 6 — подшаг 6.1 (Анализ текущего переключения)
- LevelCatalogService нигде не конфигурируется вне тестов; проект при старте всегда остаётся в legacy (Assets/Scripts/System/LevelManagement/LevelCatalogService.cs).
- Бутстрап InitGameRepositoryLoadingTask просто вызывает GameDataManager.LoadDataAsync без переключения каталога (Assets/Scripts/Entry Points/BootstrapLoadingTasks/InitGameRepositoryLoadingTask.cs:18).
- UI и менеджеры ориентируются на LevelCatalogService.IsHierarchical при построении (Assets/Scripts/System/LevelManagement/LevelSelectionModel.cs:104, Assets/Scripts/UI/Screens/SelectLevelScreenController.cs:67), но флаг не меняется.
- Миграция прогресса и LevelManager завязаны на LevelCatalogService.IsHierarchical, поэтому активация нового режима должна произойти до вызова GameDataManager.LoadDataAsync и инициализации UI.
#### Legacy
- LevelCatalogService по умолчанию активирует LegacyLevelCatalog, переключение делается вручную кодом.
- Конфигов для выбора режима нет: GameDataManager и entry points всегда работают в legacy, инициализация новой схемы происходит только через программные вызовы.
- UI/прогресс не реагируют на изменение режима в рантайме, требуется перезапуск.

#### Новый подход
- Ввести явный boolean/enum флаг (например, DayPartLevelsEnabled) в пользовательских настройках или ScriptableObject-конфиге.
- При запуске читать значение флага и централизованно переключать каталог, миграцию прогресса, UI.
- Добавить dev-инструмент для переключения на лету (консольная команда или debug UI) и события логирования для аналитики.

### Шаг 6 — подшаг 6.2 (Конфигурация флага)
#### Реализация
- SettingsData дополнен полем EnableDayPartLevels, которое сериализуется вместе с остальными настройками (Assets/Scripts/GameManagement/Data/SettingsData.cs).
- Добавлен сервис DayPartLevelsFeature для инициализации и изменения флага с сохранением через GameDataManager.SaveSettings (Assets/Scripts/System/FeatureFlags/DayPartLevelsFeature.cs).
- GameDataManager.LoadSettings вызывает DayPartLevelsFeature.InitializeFromSettings, чтобы флаг восстанавливался при запуске (Assets/Scripts/GameManagement/GameDataManager.cs).

### Шаг 6 — подшаг 6.3 (Связь флага с системами)
#### Реализация
- GameDataManager.ApplyFeatureFlags централизует применение флагов после загрузки настроек (Assets/Scripts/GameManagement/GameDataManager.cs).
- Бутстрап InitGameRepositoryLoadingTask вызывает ApplyFeatureFlags сразу после LoadSettings, до загрузки данных и инициализации UI (Assets/Scripts/Entry Points/BootstrapLoadingTasks/InitGameRepositoryLoadingTask.cs).
- Подготовлен дев-тоггл FeatureFlagToggle для быстрой смены режима в редакторе (Assets/Scripts/Debug/FeatureFlagToggle.cs).


### Шаг 6 — подшаг 6.4 (Аналитика и логи)
#### Реализация
- DayPartLevelsFeature логирует смену режима и поднимает событие OnFeatureChanged (Assets/Scripts/System/FeatureFlags/DayPartLevelsFeature.cs).
- AnalyticsManager подписывается на изменение флага и пишет событие eature_flag_change в Unity Analytics (Assets/Scripts/Analytics/AnalyticsManager.cs, Assets/Scripts/Analytics/Events.cs).
### Шаг 6 — подшаг 6.5 (QA и документирование)
#### План проверок
- Legacy режим: переустановить флаг в false, пройти выбор уровня, убедиться в корректной загрузке каталога и UI (старый поток).
- DayPart режим: включить флаг, проверить отображение времени суток, запуск уровней, корректность прогресса (звёзды/разблокировки).
- Переключение: использовать дев-тоггл или сценарий в настройках, убедиться, что миграция работает при переключении без перезапуска.
- Облачные данные: включить флаг → сохранить в облако → очистить локальные данные → убедиться, что загрузка восстанавливает новый режим.

#### Документация
- Обновить README/внутреннюю инструкцию: описать настройку EnableDayPartLevels, порядок включения и отката.
- Добавить в чек-лист релиза шаги проверки обоих режимов и логов (поисковой шаблон [DayPartLevelsFeature]).
- Для QA подготовить таблицу "режим → ожидаемый каталог/прогресс/экран".

