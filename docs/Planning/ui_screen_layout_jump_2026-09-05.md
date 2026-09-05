**Скачок размеров новых UI-экранов — расследование и исправление, 5 сентября 2026**

Основная причина — позднее применение адаптивной геометрии. Новое дерево сразу попадает в видимый контейнер с исходным размером композиции. Расчёт масштаба подключается после асинхронной загрузки фона и данных. Поэтому центр экрана может сначала показываться крупнее, затем сжиматься целиком.

Порядок выполнения подтверждён кодом и независимым анализом сабагента. Само изменение геометрии подтверждено изолированным probe в Unity 6000.2.6f2. Сквозной переход в Play Mode и длительность артефакта на устройстве не измерены.

Исходники: `integration/unity-live`, HEAD `a096e204`. Unity CLI `1.0.0-beta.8`, Pipeline `0.5.0-exp.1`. Исследуемый код и ассеты сохранены без изменений.

**Причинная цепочка на момент расследования**

1. [ScreenController.cs:51](../../LostCyberHamster/Assets/Scripts/UI/Common/ScreenController.cs) очищает текущий `content` и выполняет `CloneTree` прямо в подключённый контейнер. Готовность первого видимого кадра здесь не контролируется.
2. USS задаёт исходные размеры. Например, [shop.uss:23](../../LostCyberHamster/Assets/Content/ui/styles/components/shop.uss): frame и design размером 1725×912, исходный `scale = 1`.
3. [ScreenController.cs:76](../../LostCyberHamster/Assets/Scripts/UI/Common/ScreenController.cs) ожидает `OnLoadAsync()`. Пока операция незавершена, новое дерево уже доступно отрисовке.
4. Только затем вызывается `SubscribeToEvents()`. В [ShopScreenController.cs:71](../../LostCyberHamster/Assets/Scripts/UI/Screens/ShopScreenController.cs) подключаются `GeometryChangedEvent` и отложенный `ApplyResponsiveLayout`.
5. Расчёт задаёт `scale = min(viewportWidth / designWidth, viewportHeight / designHeight)`, новые ширину и высоту frame. Меняется масштаб всей композиции и её центрирование.

Первый geometry event может пройти ещё во время ожидания загрузки. Поздний `schedule.Execute` тогда исправляет уже показанную геометрию. Даже если загрузка завершилась синхронно, начальный размер всё равно зависит от порядка первого layout pass; отдельного условия готовности показа нет.

При возврате экран клонируется заново, inline scale предыдущего дерева теряется. Поэтому повторный вход способен повторять эффект. [UIManager.cs:67](../../LostCyberHamster/Assets/Scripts/UI/Common/UIManager.cs) сериализует навигацию через `SemaphoreSlim`; сериализация переходов сама по себе не обеспечивает правильный первый кадр.

**Охват**

Строки относятся к исходному HEAD `a096e204`; пути контроллеров — `LostCyberHamster/Assets/Scripts/UI/Screens/`.

| Экран | Что ожидается до подключения layout | Подключение / применение |
|---|---|---|
| Hero | Фон, визуальные ассеты | `CharacterScreenController.cs:129`, `:795` |
| Skills | Фон, карточки и иконки | `CharacterDevelopmentScreenController.cs:100`, `:587` |
| Select Level | Фон | `SelectLevelScreenController.cs:95`, `:400` |
| Shop | Фон, каталог предложений | `ShopScreenController.cs:71`, `:167` |
| Settings | Фон | `SettingsScreenController.cs:449`, `:487` |
| League | Фон, результаты рейтинга | `LeaderboardScreenController.cs:615`, `:340` |
| Quests | Фон, создание карточек | `QuestsScreenController.cs:111`, `:132` |

У League `OnLoadAsync` через `OpenLocationAsync` и `LoadResultsAsync` доходит до `GetResultsAsync` (`:153`, `:195`, `:271`). Таким образом, длительность ожидания первого правильного масштаба может зависеть от ответа сервиса рейтинга.

У Quests механизм другой: после загрузки включается класс `quests-screen--compact`, если высота UI меньше 760. [QuestsScreen.uss:279](../../LostCyberHamster/Assets/Content/ui/styles/screens/QuestsScreen.uss) уменьшает вкладки до 610×102, карточки до 262×328. Это тоже способно дать позднее сжатие, но только при выборе компактной раскладки.

У Home общего `ApplyResponsiveLayout` нет. [HomeScreen.uss:57](../../LostCyberHamster/Assets/Content/ui/styles/screens/HomeScreen.uss) содержит отдельные анимации кнопок: 90 мс, scale 1.02 при hover/focus и 0.97 при нажатии. Причина скачка всего центра Home этим анализом не установлена.

У модалок другой порядок: [ModalController.cs:30](../../LostCyberHamster/Assets/Scripts/UI/Common/ModalController.cs) завершает подготовку и подписки перед `display: flex`. В Journey Complete и Game Result есть аналогичные масштабируемые композиции; их первый layout после открытия требует отдельной проверки. Для Game Result намеренно используется `max` — заполнение с обрезкой; общий helper должен сохранять этот режим.

**Численная проверка в Unity**

Editor находился в Edit Mode, сцена `Bootstrap` чистая, automation idle. В существующую runtime panel временно добавлена скрытая ветка с точным `shop.uss`, `shop-viewport`, `shop-scale-frame`, `shop-design`. Выполнен layout, вызван реальный `ShopScreenController.ApplyResponsiveLayout`, повторно выполнен layout. Ветка удалена в `finally`.

| Фактический viewport, UI units | Scale до | Scale после | Ширина frame до / после | Изменение масштаба |
|---|---:|---:|---:|---:|
| 1920×1079.81 | 1 | 1.113043 | 1724.36 / 1920.00 | +11.30% |
| 1920×863.70 | 1 | 0.947036 | 1724.36 / 1633.37 | −5.30% |
| 1920×720.38 | 1 | 0.789889 | 1724.36 / 1361.90 | −21.01% |

Для второго случая позиция frame изменилась с `(97.82, −24.27)` на `(143.32, 0)`. Это согласуется с наблюдением «центр немного сужается и довыравнивается». На более высоком viewport скачок может быть увеличением. Дробные размеры — фактические значения panel после округления к пикселям.

Probe проверяет геометрию в настоящем UI Toolkit, использует исходные стили и существующий метод контроллера. Он не измеряет видимые кадры, задержку Addressables или скорость устройства. В нём не было подгрузки новых спрайтов между замерами: для изменения размеров достаточно самого `ApplyResponsiveLayout`.

**Связь с редизайном**

Поздний вызов `SubscribeToEvents` существовал раньше. Редизайн добавил в него обязательную подготовку адаптивного размера:

- 30 августа: League `4f50f491`, Quests `82e45287`, Hero `8422ac7e`, Skills `716a059a`.
- 1 сентября: Select Level `f063371c`, Shop `73a86c33`, Settings `3588ddc9`.

Спрайтовая композиция с явным исходным размером сделала старый порядок загрузки визуально заметным. Размер всего блока назначает UI-код. Анимации отдельных кнопок могут давать дополнительное небольшое движение, но не объясняют синхронное изменение frame и всех его дочерних элементов.

**Рекомендуемое исправление**

Общий контракт первого показа разместить в `ScreenController`; общий расчёт масштабируемой композиции — в UI helper. Применить к шести экранам с design/frame и к компактной раскладке Quests.

1. После создания дерева синхронно получить ссылки на элементы и подключить расчёт геометрии, до первого `await OnLoadAsync`.
2. Держать новую композицию скрытой до валидного размера и завершения layout с применёнными scale, frame и compact-классами. Для этого подходит `visibility: hidden`: геометрия продолжает рассчитываться. Точки управления видимостью дочерних элементов проверить вместе с этим контрактом.
3. Принимать размеры только конечные и больше нуля. Сохранять предыдущую валидную геометрию при временно некорректных значениях. Текущий `Mathf.Max(1f, size)` превращает нулевую геометрию в почти нулевой масштаб и не является проверкой готовности.
4. Показывать композицию после применения корректной геометрии. Готовность масштаба отделить от готовности каталога, иконок и ответа рейтинга; данные заполняют уже размеченные места.
5. Подписки действий и сервисов подключать после подготовки их зависимостей. Простая перестановка всего `SubscribeToEvents()` вперёд рискованна: он содержит больше, чем layout.
6. Привязать callbacks и scheduled items к конкретному дереву; при detach отменять ожидание готовности. Сохранить реакцию на изменение размера окна.

`GeometryChangedEvent` сообщает об изменении позиции или размера; подписка после события не получает уже прошедшее изменение. [Unity 6.2: Layout events](https://docs.unity3d.com/6000.2/Documentation/Manual/UIE-Layout-Events.html). Скрытый через `visibility` элемент сохраняет участие в layout. [Unity 6.2: Visibility](https://docs.unity3d.com/6000.2/Documentation/ScriptReference/UIElements.Visibility.html).

**Пользовательская визуальная приёмка**

- Записать первые кадры входа и повторного входа в семь экранов. С первого видимого кадра frame, scale и compact-раскладка уже итоговые.
- Проверить 16:9 и широкие landscape-пропорции, Quests по обе стороны высоты 760 UI units, изменение размера окна.
- Проверить первый и повторный вход, медленную загрузку ассетов, задержку рейтинга, быстрые последовательные переходы.
- Проверить центр, HUD, заголовок и кликабельные области. HUD/заголовок сохраняют существующую систему координат вне scale-frame.
- Отдельно проверить Home и первый кадр Journey Complete / Game Result.

На этапе расследования изменены только этот отчёт и запись опыта в `docs/experience/existing_systems.md`. Исходные чужие изменения HUD, `GameScreen.uxml`, кнопки паузы, `error_diagnosis.md` и `tool_usage.md` сохранены. C# build, recompile, Play Mode и сборки не запускались.


**Реализация**

- `ScreenController` готовит отдельное скрытое дерево; `ScreenLayout` рассчитывает scale/frame либо compact-класс и ждёт применения геометрии panel. Фиксированной временной паузы нет.
- `UIManager` сохраняет старый экран и фон до готовности нового. Переключение фона, bindings и содержимого выполняется синхронно. При ошибке подготовки остаётся прежний экран; при ошибке bindings восстанавливаются его подписки.
- `PreparedScreen` удерживает UXML и фон до удаления своего дерева. Фон применяется к существующему полноэкранному host, композиция остаётся в контейнере safe area.
- `BindView` размечает локальные данные и слоты до показа. `LoadDataAsync` отдельно загружает Hero/Skills icons, каталог Shop и результаты League. Отмена или версия запроса защищает от устаревших ответов.
- Новый контракт подключён к Home, Hero, Skills, Select Level, Shop, Settings, League и Quests. Game/Intro сохраняют активацию дерева только в момент показа.
- Геометрия продолжает обновляться при resize. При закрытии panel снимаются callbacks, освобождаются leases и отменяется ожидание готовности.

Независимое ревью кода выполнено сабагентом: lifecycle, повторный вход/Repaint, загрузки данных и ресурсы. Визуальная приёмка после реализации остаётся для пользовательского запуска.


**Выполненные проверки**

`regenerate_project_files` выполнен через проектную Unity MCP-команду. `dotnet build Assembly-CSharp.csproj --no-restore`: exit 0, 0 ошибок, 36 предупреждений. Generated project включает `ScreenLayout.cs` и `PreparedScreen.cs`; их `.meta` созданы Unity. Финальный gate выполнен без Play Mode и принудительного Unity recompile.

**Собственные файлы реализации**

В `LostCyberHamster/Assets/Scripts/UI/`:

- `Common/ScreenController.cs`, `Common/UIManager.cs`.
- `Common/PreparedScreen.cs`, `Common/ScreenLayout.cs` и их `.meta`.
- `Screens/HomeScreenController.cs`, `Screens/CharacterScreenController.cs`, `Screens/CharacterDevelopmentScreenController.cs`.
- `Screens/SelectLevelScreenController.cs`, `Screens/ShopScreenController.cs`, `Screens/SettingsScreenController.cs`.
- `Screens/LeaderboardScreenController.cs`, `Screens/QuestsScreenController.cs`.
- `Screens/GameScreenController.cs`, `Screens/IntroScreenController.cs` — синхронный контракт bindings.

Также `LostCyberHamster/Assembly-CSharp.csproj`, этот отчёт и секция «Готовность первого UI-кадра» в `docs/experience/existing_systems.md`.

Внешних блокеров компиляции нет. Чужие изменения базы опыта, offline-аудита и редизайна четырёх зон сохранены вне коммита этой задачи.
