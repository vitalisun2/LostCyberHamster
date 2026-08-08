# Product Content Delivery — целевая концепция

## 1. Цель продукта

Один проверяемый канал доставки обновляет art, data и prefab-content без новой версии приложения. Локальный player содержит код, core scenes и безопасный минимальный контент. CDN содержит совместимые версии изменяемых ассетов.

Основной контракт:

- код определяет доступные механики и схемы;
- remote catalog определяет доступные assets и data;
- content manifest подтверждает совместимость с версией приложения;
- активная игровая сессия использует один зафиксированный content release.

## 2. Runtime-поток

1. Локальный Bootstrap запускает Addressables.
2. `ContentDeliveryService` проверяет remote catalog и content manifest.
3. Совместимый catalog обновляется до инициализации skins, localization, quests и levels.
4. Обязательный минимальный пакет загружается с размером и прогрессом.
5. Остальной контент грузится по domain/address при открытии экрана или выборе уровня.
6. Scope-владелец держит leases и освобождает их при закрытии экрана, смене уровня или завершении забега.
7. Новая версия каталога применяется только при следующем bootstrap; активный уровень не hot-swap'ится.

Offline fallback: локальный catalog и минимальный набор позволяют открыть игру. Успешно скачанный совместимый release остаётся в cache. Несовместимый remote release не активируется.

## 3. Состав releases

| Слой | Содержимое | Политика |
|---|---|---|
| Local Core | Bootstrap/Menu/Game scenes, DI, gameplay prefabs, default UI shell, обязательные shaders/material baseline | Обновляется вместе с приложением. |
| Remote Catalogs | Manifest, localization, skins, quests, shop, super attacks, locations, mappings | Малые bundles; обновляются до domain assets. |
| Remote World | Level JSON, patterns, environment, decor, obstacles, intro | Разделение по локации; preload выбранной локации. |
| Remote Cosmetics | Skin visual prefabs, clips, sheets, previews | Один bundle на скин; on-demand или preload выбранного. |
| Remote UI | UXML/USS/art существующих screens и modals | По root UXML; local shell остаётся fallback. |
| Remote Media | Music, будущий SFX, тяжёлые effect assets | По треку/банку/эффекту; on-demand. |

Bundles собираются отдельно для Android, iOS, Windows и WebGL. Catalog release всегда ссылается на bundles той же платформы и baseline player version.

## 4. Категории и авторский workflow

| Категория | Где редактируется | Единица публикации | Условие live-обновления |
|---|---|---|---|
| Level data | `Assets/Content/locations/<location>/levels/` | Level JSON или пакет daypart | Валидная схема, address и `levels_daypart` label |
| Patterns | `Assets/Content/locations/level_design_templates/levels/PatternsCollection.json` | Общий catalog bundle | Все refs разрешаются; semantics уже поддержаны кодом |
| Environment | `Assets/Content/locations/<location>/sprites/backgrounds/` | Location/daypart package | Стабильные `bg`, `bg_2`, `sky`, `rd` addresses |
| Decor | Папка sprites конкретной локации | Location decor package | Sprite name совпадает с Level JSON; label корректен |
| Obstacles | Sprites, clips и mapping конкретной локации | Location obstacle package | Известный archetype, размеры, pivot, prefab/Animator contract |
| Intro | Intro sprites рядом с level content | Пакет уровня | Адреса `intro_01...10`, без разрывов |
| Skins | `Assets/Content/prefabs/skins/<slug>/`, `Assets/Animations/Hamster/skin_visuals/<slug>/`, `Assets/Content/skins/<slug>/` | Root prefab, `Pack Separately` | Уникальный ID/address, полный semantic mapping, preview/localization |
| UI | `Assets/Content/ui/uxml`, styles и art | Root UXML screen/modal | Стабильные element names и controller contract |
| Localization | `Assets/Content/localization/` | Один малый bundle на locale | Полный набор обязательных keys и fallback locale |
| Quests | `Assets/Content/quests/questData.json` | Catalog bundle | Только известные types/actions/states/rewards |
| Shop | `Assets/Content/shop/shopItems.json` + images | Catalog + item art | Известные reward/payment semantics и localization key |
| Super attacks | Catalog, icons и effect prefabs | Catalog + prefab per attack | ID связан с существующим runtime archetype |
| Audio | `Assets/Content/audio/` | Один трек или audio bank | Роль и address присутствуют в audio manifest |
| Bonuses/effects | Existing root prefabs и dependencies | Prefab per archetype | Только compiled behavior и сохранённый component contract |

Публикуется Addressable entry, а не папка сама по себе. Controller, clips, sprites, USS и textures входят как dependencies root asset.

## 5. Group и packing policy

| Группа | Packing |
|---|---|
| `Content Catalogs` | `Pack Together` для малых атомарно совместимых JSON. |
| `Levels <location>` | По daypart или `Pack Separately` для Level JSON. |
| `Environment <location>` | `Pack Together` внутри одной загружаемой локации. |
| `Obstacles <location>` | Отдельно от environment; sprites/clips/mapping одной локации вместе. |
| `Skin Visuals` | Сохранить `Pack Separately`, explicit entry только root prefab. |
| `UI Content` | `Pack Separately` по root UXML либо малые screen-пакеты. |
| `Audio` | `Pack Separately` по треку; будущие короткие SFX можно паковать банком. |
| `Effects / Bonuses` | Root prefab как единица загрузки. |

Shared dependency допускается только для стабильного общего ресурса. Общий atlas на все локации или скины связывает их загрузку и обновление.

## 6. Compatibility contract

`ContentManifest`:

```text
ReleaseId
ContentSchemaVersion
MinimumAppVersion
Platform
RequiredCatalogVersion
MandatoryPackages[]
OptionalPackages[]
```

Общие правила:

- address и ID стабильны после первого release;
- удаление сохраняемого ID требует data migration;
- JSON имеет schema version и проходит validator;
- prefab использует только доступные в player compiled components;
- UI сохраняет имена элементов, которые ищет controller;
- animation clips сохраняют параметры, states и event contract;
- новые данные с неизвестным type/action/archetype отклоняются до publish;
- catalog, JSON и связанные assets публикуются одной совместимой версией.

## 7. Загрузка, cache и lifetime

| Scope | Что держит | Когда освобождает |
|---|---|---|
| Application | Manifest, localization, малые meta-каталоги | При shutdown или смене release |
| Menu screen | UXML, background, previews/icons | При закрытии экрана; общий cache может оставить hot assets |
| Location | Environment, decor, obstacle sprites/clips/mappings | При выходе из локации |
| Level | Level JSON, intro, временные dependencies | После parse/завершения уровня |
| Run | Выбранный SkinVisual, super-attack/effect prefabs | При уничтожении Hamster/Game scene |

Перед загрузкой показываются размер и прогресс. Retry ограничен. Cache имеет quota и LRU-cleanup только для неактивных releases. `Addressables.Release` вызывается через единый lease/owner pattern.

## 8. Publish pipeline

1. Выбрать baseline player release и его `addressables_content_state.bin` для платформы.
2. Запустить content validators: address/label, JSON schema/refs, localization, prefab, dimensions, dependencies и bundle size.
3. Построить content update относительно baseline.
4. Загрузить hashed bundles, catalog, hash и manifest в immutable staging path.
5. Проверить clean install, cached update, offline fallback и representative content load.
6. Атомарно продвинуть release в production channel.
7. Сохранить предыдущий catalog/release для rollback.

Dev, Staging и Prod имеют разные profile variables и channels. Full player build создаёт новый baseline; последующие art/data releases строятся только от сохранённого state этого baseline.

## 9. План покрытия

| Этап | Результат |
|---|---|
| 1. Transport | Production profiles, remote catalog, bootstrap update, manifest и offline fallback. |
| 2. Pilot | Skin Visuals публикуются и обновляются через Staging/Prod без player rebuild. |
| 3. World | Level JSON, patterns, environment, decor, obstacles и intro разделены по location packages. |
| 4. Meta/UI | Localization, quests, shop, super attacks и существующие UI screens получают compatibility validators. |
| 5. Media/lifetime | Audio catalog, preload/progress/cache policy; старые raw loads переведены на leases. |
| 6. Operations | CI build/upload/promote/rollback и monitoring release ошибок/размеров. |

## 10. Критерий полного покрытия

Без app update выпускаются:

- новые уровни и art-наборы в существующей gameplay-схеме;
- новые локации при полном совместимом content package;
- новые скины с текущим semantic contract;
- новые квесты и товары известных типов;
- новые visual-варианты obstacles, bonuses, effects и super attacks известных archetypes;
- обновлённые UI layout/art, localization и audio из опубликованных catalog contracts.

App update остаётся каналом для нового кода, новых gameplay contracts, core scenes и platform integration.
