# Мини-план сервиса Editor Addressables

## Цель
Ввести отдельный сервис, который отвечает за работу с Addressables в редакторе (спрайты и JSON), разгрузив `LevelTilemapUi`, `LevelDataManager` и устаревшие загрузчики, а также централизовав управление lease и нормализацией локаций.

## Задачи
1. **Каркас сервиса**
   - Создать `EditorAddressablesService` (пространство имен `Assets.Editor.LevelEditor.AddressablesSupport`).
   - На первом этапе использовать статический вход (позже можно перейти на DI).

2. **API загрузки спрайтов**
   - Метод `LoadObstacleSprites(string location)` возвращает `AddressableSetLease<Sprite>` и скрывает fallback для шаблонов и формирование лейблов.
   - Подготовить перегрузки для других категорий (collectables, decor), чтобы облегчить будущие миграции.

3. **API для JSON (маппингов)**
   - Методы `LoadObstacleMappings(string location, Action<Dictionary<string, ObstacleTypeEnum>> onLoaded)` и `SaveObstacleMappings(string location, Dictionary<string, ObstacleTypeEnum> bindings)` инкапсулируют текущую логику `LevelDataManager`.
   - Централизовать построение ключей/лейблов и регистрацию ассетов.

4. **Хелперы локаций**
   - Предоставить `ResolveLocation(string location, AddressableAssetType type)`, чтобы спрятать логику fallback (Templates → New York) и будущие переопределения.
   - Оставить возможность переиспользования в рантайме.

5. **Шаги миграции**
   - Обновить `LevelTilemapUi`, чтобы использовать `EditorAddressablesService.LoadObstacleSprites` вместо встроенного кода.
   - Перенести работу с Addressables в `ObstacleSpriteTypeMappingsManager` и `LevelDataManager` на новый сервис, оставив им UI/состояние.
   - Проанализировать оставшиеся вызовы `SpriteLoader` и наметить его вывод из проекта после миграции потребителей.

6. **Тестирование и проверка**
   - Добавить редакторские тесты или тестовый стенд для проверки lease и JSON round-trip.
   - Выполнить ручной smoke-тест для переключения локаций и сохранения маппингов в Level Tilemap Editor.

## Риски и особенности
- Убедиться, что сервис корректно освобождает ресурсы (при необходимости — предоставить вспомогательные методы `Release`).
- Учесть существующее поведение кэша; при надобности добавить тонкий кэш внутри сервиса.
- Согласовать именование методов с текущим `AddressableLoader`, чтобы избежать путаницы.
