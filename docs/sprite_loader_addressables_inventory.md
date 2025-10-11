# Инвентаризация точек загрузки Addressables

Документ отражает текущее состояние использования Addressables в проекте (рантайм и редактор), с акцентом на загрузку спрайтов и связанных ресурсов. Для каждого места отмечены типы ассетов, способ доступа, текущее управление `AsyncOperationHandle` и риски.

## Рантайм

**LevelDataProvider (`Assets/Scripts/System/LevelManagement/LevelDataProvider.cs`)**
- Загружает: спрайты (препятствия, декор, collectables, интро), `TextAsset` описаний, игровые префабы, эффекты.
- Методы: `Addressables.LoadAssetsAsync` по лейблам, `LoadAssetAsync` по адресам.
- Управление handle: bulk-спрайты освобождаются сразу после получения списка; интро-спрайты аккумулируются во внутренних списках и освобождаются явным методом; одиночные `LoadAssetAsync` в большинстве случаев не освобождаются.
- Риски: возможный преждевременный выгруз спрайтов, утечки при загрузке префабов.

**UI-компоненты (`Assets/Scripts/UI/...`)**
- `BaseStorageUI`, `Healthbar`, `Energybar`, `ScreenController`, `ModalController`, `ResourceUIHelper`, `ShopModalController`, `CharacterScreenController` и пр.
- Загружают `VisualTreeAsset`, `Sprite`, `Texture2D`.
- Управление handle неоднородно: где-то релиз делается сразу после загрузки (`VisualTreeAsset`), где-то отсутствует вовсе, либо используется `Completed +=` без последующего `Release`.

**Метасистемы (`Assets/Scripts/SharedCore/Meta/...`)**
- `QuestManager`, `ShopManager`, `SkinFactory`, `SkinManager`.
- Загружают `TextAsset`, `Sprite`, `GameObject`, `RuntimeAnimatorController`.
- Release отсутствует: потенциальные долгоживущие ссылки на ресурсы.

**Каталоги и конфигурация (`System/LevelManagement/LevelCatalogRuntimeConfigurator.cs`, `LevelManager.cs`)**
- Используют `LoadResourceLocationsAsync` и `LoadAssetAsync` для JSON/каталогов.
- Release выполняется корректно; положительный пример.

**Bootstrap (`Entry Points/BootstrapLoadingTasks`)**
- Инициализация Addressables и выборочные загрузки (`AudioClip`).
- Release частично отсутствует.

## Редактор

**SpriteLoader (`Assets/Editor/LevelEditor/SpriteLoader.cs`)**
- Загружает спрайты по ключам и лейблам.
- Хэндлы кешируются в `_handleCache`, есть методы для очистки и release.
- Хороший кандидат на замену общим сервисом, чтобы избежать дублирования логики с рантаймом.

**LevelDataManager (`Assets/Editor/LevelEditor/LevelDataManager.cs`)**
- Читает JSON-мэппинги спрайтов препятствий из Addressables.
- Для `LoadResourceLocationsAsync` и `LoadAssetAsync` вызывает `Release`; поведение корректное.

**LevelAssetsAddressableSync**
- Манипулирует настройками Addressables, но не загружает ресурсы.

**Прочие редакторские утилиты**
- Работают через `AssetDatabase`/`Directory`, Addressables напрямую не трогают.

## Плагины / сторонний код

- `Assets/Plugins/Sirenix/.../Unity.Addressables` — инспекторы и валидаторы Odin Inspector; в рамках рефакторинга рассматриваются как внешние и не требуют изменений.

## Выводы

1. Максимальные риски сконцентрированы в `LevelDataProvider` и части UI/мета-классов (отсутствует единая стратегия release).
2. Редакторский `SpriteLoader` уже содержит кеширование и ручной `Release`, но логику нужно упростить и унифицировать с рантаймом.
3. Помимо спрайтов, имеет смысл охватить префабы и текстуры, чтобы новое решение отвечало за все типы Addressables-ресурсов с понятным жизненным циклом.

## Классификация по времени жизни

- **Пер-уровневые ресурсы**: препятствия, декор, интро-спрайты, фоновые текстуры уровня, эффекты уровня. Загружаются `LevelDataProvider`, должны освобождаться при выгрузке уровня.
- **Сессионные ресурсы**: UI-иконки валют, глобальные Shop/Quest/Skin данные, префабы бонусов. Используются на протяжении всей игровой сессии; рекомендуется централизованное хранение и осознанный release при выходе из игры или очистке менеджера.
- **Редакторские краткоживущие**: спрайты для палитры/превью в LevelEditor. Должны очищаться при смене локации, закрытии окна или явном сбросе кэша.
- **Редакторские долгоживущие**: JSON маппинги препятствий, конфигурации уровней. Считаются «константами», но всё равно должны иметь понятный механизм release при обновлении.

## Классификация по схемам лейблов и ключей

- **Локация + постфикс**: основная схема для спрайтов препятствий/декора/коллектаблов (`"{location} obstacles sprites"`, `"{location} decor sprites"`). Требует явного указания локации и fallback-локации.
- **Глобальные лейблы**: `Consts.CollectableSpritesLabel`, `levels_daypart`, и др. Используются для ресурсов, общих для всех локаций или частей суток.
- **Прямые адреса**: `skip_button`, `shopItems.json`, `questData`, `levelAddress`. Строки формируются вручную; зачастую отсутствует промежуточный хелпер.
- **Динамические построения**: intro-спрайты (`"{baseAddress}/intro_{index}"`), background ключи (`bg_{location}_{part}`) — требуют унифицированных функций построения.
