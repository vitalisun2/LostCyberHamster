# Product Content Delivery — анализ

## 1. Вывод

Addressables уже покрывает почти весь изменяемый контент: уровни, окружение, препятствия, UI, локализацию, скины, meta-каталоги, музыку и эффекты. Сейчас это локальная упаковка внутри билда, а не доставка по воздуху.

Для Content Delivery нужны три базовых блока:

1. Remote-профили и remote catalog.
2. Runtime-обновление каталога до загрузки контента.
3. Версионируемый build/upload/promote/rollback pipeline по каждой платформе.

Без нового C# можно доставлять новые данные и ассеты только внутри уже скомпилированных контрактов. Новый gameplay-тип, компонент, enum или формат данных требует обновления приложения.

## 2. Текущее состояние Addressables

| Блок | Факт | Последствие |
|---|---|---|
| Версия | Addressables `2.7.2` | Поддерживает remote catalog и content update. |
| Профили | Только `Default` | Нет Dev/Staging/Prod и production CDN URL. |
| Remote catalog | Выключен; build/load paths пусты | Приложение не получает новый каталог. |
| Remote Load Path | Локальный hosting URL через `PrivateIpAddress` | Для production не используется. |
| Группы | Все Bundled groups используют Local Build/Load | Bundles входят в player build. |
| Content Update | Группы помечены `Can Change Post Release` | Конфигурация допускает обновление контента. |
| Packing | Почти везде `Pack Together` | Малое изменение часто обновляет bundle всей группы. |
| Skin Visuals | `Pack Separately` | Один скин образует отдельную единицу загрузки. |
| Cache / CRC / compression | Включены cache, CRC, LZ4 | Подходящая база для remote bundles. |
| Bootstrap | Нет рабочего явного `CheckForCatalogUpdates` / `UpdateCatalogs` | Политика обновления, ошибки и прогресс не контролируются. |

`LoadAddressablesLoadingTask` вызывает `InitializeAsync()` дважды, не использует перечисленные ключи и не подключён в `BootstrapSceneInstaller`. Сейчас Addressables инициализируется неявно первым запросом контента.

`levels` и `levels_by_daypart` используют одни schema assets, причём schema ссылается на группу `levels`. Конфигурацию нужно разделить до remote-публикации.

## 3. Карта контента

| Категория | Текущий источник и загрузка | После подключения CDN | Граница без обновления приложения |
|---|---|---|---|
| Уровни | Level JSON обнаруживаются по label `levels_daypart`; адрес задаёт location/daypart/level | Менять и добавлять уровни | Только текущая JSON-схема, tile/pattern semantics и gameplay actions |
| Patterns | Общий Addressable `PatternsCollection` | Менять и добавлять шаблоны | Новый тип элемента или новое поведение требует C# |
| Локации | `locations.json`, preview и level catalog — Addressables | Менять metadata/art; добавлять полную локацию после contract validator | Нужен комплект levels, mappings, labels, environment, localization; порядок metadata сейчас связан с runtime-каталогом |
| Фоны, второй фон, небо, дорога | Addressable sprites по location/daypart keys | Заменять и добавлять наборы окружения | Уникальный art отдельного уровня не выбирается: runtime использует location/daypart |
| Environment prefab | `ScrollingEnvironmentPrefab` — Addressable | Менять layout и art dependencies | Сохранять ожидаемые компоненты и compiled scripts |
| Декор | Sprites по location-label; объекты строятся из Level JSON | Заменять и добавлять декор без prefab | Только visual: новое поведение требует gameplay archetype |
| Intro | Последовательность адресов `<level>/intro_01...10` | Менять и добавлять до 10 кадров | Непрерывная нумерация; новый сценарий показа требует C# |
| Obstacle art | Sprite mappings и labels по локации | Перерисовывать и добавлять варианты | Только существующие `ObstacleTypeEnum` и размеры/collider contract |
| Obstacle animation | Addressable clips, текущие semantics `idle`/`walk` | Менять frames, FPS и варианты | Новое animation/gameplay state требует C# |
| Obstacle prefabs | Три generic Addressable prefab | Менять art/layout при сохранении contract | Новый archetype и механика требуют C# |
| Bonuses / effects | Prefabs и sprites Addressable, адреса загрузки фиксированы | Перерисовывать существующие типы | Новый bonus/effect type требует registry и C# |
| Скины | Remote-ready catalog model + `Skin Visuals`; четыре prefab-addresses, `Pack Separately` | Менять и добавлять skin prefab, clips, sheets, preview, price | Visual использует девять известных semantic actions; новая gameplay semantics требует C# |
| Суперудары | JSON, icons и effect prefabs Addressable | Балансировать и менять art существующих ID | Runtime factory поддерживает только ID `1` и `2`; новая механика требует C# |
| UI screens / modals | UXML загружаются по Addressables; USS/art — dependencies | Менять art, style и layout существующего экрана | Имена queried elements и controller contract стабильны; новый экран требует C# registration |
| UI shell | Root UIDocument и PanelSettings заданы сценами | Остаётся локальным безопасным bootstrap | Его замена требует отдельной remote-shell архитектуры или app update |
| Localization | `lang.en` и `lang.ru` — Addressable JSON | Менять и добавлять ключи | Новый locale требует проверки внешнего `LocalizationManager` и locale manifest |
| Quests | `questData` строит Daily-каталог динамически | Менять и добавлять квесты известных типов/actions/states | Новые strategy/type/action/state и reward semantics требуют C# |
| Shop | `shopItems.json` и item art — Addressables | Менять и добавлять товары известных reward/payment типов | Новая валюта или операция требует C#; item localization contract неполный |
| Audio | `music_test1/2` — Addressables | Заменять используемый `music_test1` | Playlist, новые роли музыки и SFX требуют audio catalog/router |
| Core scenes | `Bootstrap`, `Menu`, `Game` входят в Build Settings | Остаются локальным каркасом | Scene composition, DI, gameplay pipeline обновляются с приложением |
| Resources | Tutorial finger и bootstrap-служебные assets локальны | Не обновляются через каталог | Перенести нужный art в Addressables |

## 4. Что уже хорошо подготовлено

- Все 91 JSON под `Assets/Content` зарегистрированы в Addressables: level data, patterns, locations, mappings, localization и meta-каталоги.
- `LevelCatalogRuntimeConfigurator` обнаруживает level assets по label, поэтому список уровней не зашит в приложение.
- Level art разделён на environment, obstacles, decor и intro; большая часть выбирается по address/label.
- Новый Skin Remake убрал whitelist ID. `SkinVisualAddress` приходит из JSON; выбранный prefab загружается с fallback на default.
- `SkinVisualRuntime` и super attacks используют `AddressableLoader` и lease с явным освобождением.
- Editor sync уже добавляет новые level JSON/intro и labels в Addressables.

## 5. Текущие технические долги

| Приоритет | Проблема | Рекомендация |
|---:|---|---|
| P0 | Нет production remote catalog/CDN profiles | Добавить Dev/Staging/Prod, platform/version paths и remote groups. |
| P0 | Нет release pipeline и надёжного baseline content state | Хранить state каждого player release; строить content update только от него. |
| P0 | Нет атомарного publish/rollback | Загружать immutable release, валидировать, затем переключать active catalog; хранить предыдущий release. |
| P0 | Нет compatibility gate | Ввести content manifest с schema version, minimum app version и release ID. |
| P1 | Runtime не управляет catalog update, offline fallback и прогрессом | Ввести один bootstrap `ContentDeliveryService` до всех catalog consumers. |
| P1 | Много прямых `LoadAssetAsync` без владельца/release | Перевести загрузки на leases; JSON освобождать после parse, art держать срок экрана/уровня. |
| P1 | `Pack Together` связывает большие домены | Делить bundles по release/load unit: локация, скин, экран, трек, каталог. |
| P1 | Нет проверки адресов, labels, JSON refs и prefab contracts перед публикацией | Добавить единый content validator и remote smoke test. |
| P1 | `SkinVisualContentValidator` перечисляет только четыре текущих slug | Строить проверку из `skins.json` и Addressables entries, чтобы новый скин тоже проходил contract gate. |
| P2 | Нет preload/download-size UX, retry/timeouts и cache policy | Добавить обязательные пакеты, on-demand загрузку, прогресс и лимит диска. |
| P2 | Tutorial art, UI shell и часть scene refs локальны | Явно закрепить local core; переносить только действительно live-content assets. |
| P2 | Legacy AssetBundles/CDN код существует параллельно | Архивировать после подтверждения отсутствия runtime callers. |

## 6. Жёсткая граница Content Delivery

Content Delivery может обновить asset или prefab с уже скомпилированными компонентами. Он не доставляет новый C# код.

Обновление приложения требуется для:

- нового MonoBehaviour, gameplay-механики или DI binding;
- нового enum/type/strategy, который runtime ещё не знает;
- несовместимого JSON schema или save schema;
- изменения core scenes и bootstrap pipeline;
- нового native plugin/package;
- shader/variant, не включённого и не проверенного для player build;
- изменения gameplay Transform Animation events хомяка.

Visual sprites, Sprite Animation, audio, data, UXML и prefab composition обновляются по воздуху, если сохраняют опубликованный contract.
