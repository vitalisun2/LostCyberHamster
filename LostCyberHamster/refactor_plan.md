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

## Рабочие заметки

### Шаг 2 — анализ текущих Addressables
- `Assets/Content/locations/<Location>/levels/level_XX.json` — на текущий момент по четыре файла на локацию (01_New_York и 02_Paris), уровни идут последовательно `level_01…level_08`.
- Addressables-группа `Assets/AddressableAssetsData/AssetGroups/levels.asset` содержит те же восемь уровней с адресами `level_01…level_08` и единым лейблом `levels`.
- `LevelDataProvider.LoadLevelInfo` грузит `TextAsset` по адресу, совпадающему с `GameDataManager.PlayerData.CurrentLevel` (строка вида `level_XX`).
- Созданы подпапки `Morning/Afternoon/Evening/Night` в каждой локации с копиями соответствующих `level_XX.json` и новыми `.meta` GUID.
- Создана Addressables-группа `Assets/AddressableAssetsData/AssetGroups/levels_by_daypart.asset` с адресами `<Location>/<Part>/level_XX` и метками `levels_daypart`, `levels_daypart_<Part>`, `levels_location_<Location>`.

\n### Step 2 Notes\n- Для новой схемы уровни размещаются параллельно в подпапках `Morning/Afternoon/Evening/Night`, исходные файлы сохраняются в корне `levels/`.
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
