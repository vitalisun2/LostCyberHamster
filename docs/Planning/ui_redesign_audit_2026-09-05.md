**Аудит редизайна UI — 05.09.2026**

Основные меню уже переведены на рисованный дизайн. Осталось **шесть действующих зон со старым оформлением**: выбор сохранения, обучение, повышение уровня, общая ежедневная награда, оболочка сюжетного интро и сообщение «Нет сети». Отдельный мелкий хвост — кнопка повторной загрузки рейтинга.

**Предложение привязать аккаунт уже обновлено** вместе с Settings. Для Cloud Save Conflict и Tutorial художественные наборы подготовлены в WorkOnScreens, но их основные элементы ещё не подключены в игре.

Проверены: история `integration/unity-live` за 29.08–05.09.2026, текущие UXML/USS/C#, регистрации и места вызова, сцены, `Content/ui/sprites`, папка WorkOnScreens. Срез: HEAD `a096e204`, рабочее дерево на 05.09.2026 около 14:10 МСК. Это статический аудит с просмотром исходных макетов; фактическая отрисовка всех состояний в Play Mode здесь не проверялась.

**Что осталось**

| Очередь | Экран / состояние | Почему это действующий хвост | Готовность дизайна и следующий шаг |
|---|---|---|---|
| 1 | **Выбор облачного или локального сохранения — CloudSaveConflictModal** | Координатор показывает окно при текущем конфликте. Карточки — цветные контейнеры с рамками; кнопки — прежние `lcs_btn`. Сохранена старая оболочка `Modal.uxml`. [Показ][cloud-call], [стили][cloud-style]. | Есть [макет][cloud-preview] и [пакет][cloud-pack]: 6 отдельных слоёв + экран-образец. Подключить панели, заголовок, карточки и две кнопки. Перед сборкой сверить manifest: он ещё описывает составную карточку, уже разделённую на panel/divider. |
| 2 | **Обучение — заголовок, Skip, подсказки и указатель** | UI создаёт `TutorialGameplayView`: плоские панели с radius, программные кнопки, прежний ресурс пальца. Активные шаги используют этот view. [Создание UI][tutorial-view], [стили][runtime-style]. | Есть [пакет Tutorial][tutorial-pack]: 13 игровых слоёв + 2 экранных образца, включая завершение. Подключить элементы активного обучения. [QA][tutorial-qa] содержит актуальный состав. Фон завершения уже переиспользован в игровых модалках; это не означает миграцию Tutorial. |
| 3 | **Повышение уровня — LevelUpModal** | После результата игры при росте PlayerLevel показывается отдельное окно перед выбранным переходом. Заголовок, переход уровней, Development Points и OK остаются в старой оболочке. [Триггер][levelup-call], [шаблон][levelup-ui], [стили][levelup-style]. | Отдельного готового набора в WorkOnScreens не найдено. Подготовить компактную композицию в общем стиле наград, используя существующие рисованные панели и кнопки. |
| 4 | **Общая награда за ежедневные задания — DailyQuestRewardModal** | На вкладке Daily при `CanClaimDailyCommonReward` окно планируется через 1 секунду. Сам Quests обновлён, но это отдельная модалка со старой цветной кнопкой. [Триггер][daily-call], [шаблон][daily-ui], [стили][daily-style]. | Отдельного набора не найдено. Собрать рисованное окно с заголовком, суммой, иконкой валюты и действием получения. |
| 5 | **Оболочка сюжетного интро уровня** | Живой `Intro.cs` показывает сюжетные картинки, но вокруг них остаётся `intro-surface`; Skip использует обычный `lcs_btn`. [Создание оболочки][intro-view], [Skip][intro-skip]. | Отдельного UI-набора не найдено. Доработать фон/оболочку и Skip. Сюжетные иллюстрации — самостоятельный контент. |
| 6 | **«Нет сети» — No Network** | Активный `LicenseManager` в Menu при отсутствии сети добавляет полноэкранную подложку и два обычных Label. Текст английский, задан прямо в коде. [Триггер][network-call], [вид][network-view], [объект сцены][network-scene]. | Отдельного макета не найдено. Оформить служебное сообщение общими рисованными компонентами и локализовать текст. |

**Мелкий хвост внутри обновлённого экрана:** в League у ошибки загрузки есть обычная кнопка Retry. Для неё заданы размеры и шрифт, но отсутствует художественная основа. Обновить кнопку при сохранении существующего сценария повтора. [Шаблон][league-retry-ui], [стили][league-retry-style]. Остальной League уже переведён.

**Полная сверка экранов**

Статус «обновлён» означает: новый художественный слой подключён в текущих исходниках. Native-текст, обычные контейнеры и отсутствие отдельной папки ассетов сами по себе не означают пропущенный редизайн.

| Экран / семейство | Статус | Основание |
|---|---|---|
| HomeScreen | Обновлён | `home` + shared HUD, `7c48c321` |
| CharacterScreen — Hero, skins/abilities | Обновлён | `hero`, `54cde579` + интеграция `8422ac7e` |
| CharacterDevelopmentScreen — Skills | Обновлён | `skills`, `716a059a`; дальнейшая полировка карусели и прогресса |
| QuestsScreen — daily/story | Обновлён | `quests`, `82e45287`; отдельная общая награда остаётся выше |
| SelectLevelScreen — выбор времени суток и уровня | Обновлён | Обе композиции используют `select_level`, `f063371c` |
| LeaderboardScreen — League | Обновлён, остался Retry | `league`, `4f50f491` |
| ShopScreen | Обновлён | Полноэкранный магазин, `73a86c33`; прежний ShopModal удалён |
| SettingsScreen | Обновлён | `settings`, `3588ddc9`; включая язык, гостевой аккаунт и редактирование имени |
| AccountPromptModal | Обновлён | Тот же `3588ddc9`; рисованные панели и кнопки Settings, [актуальные стили][account-style] |
| CloudSaveConflictModal | Старый | Действующий хвост № 1 |
| GameScreen — HUD | Обновлён; Pause в текущей работе | `in_game_hud`, `c59e9164`; новая кнопка Pause пока в незакоммиченном diff |
| PauseModal | Обновлён | `modals/pause`, `77988aa9` |
| WinModal | Обновлён | `modals/win`, `77988aa9` |
| LoseModal | Обновлён | `modals/fail`, `77988aa9` |
| JourneyCompleteModal | Обновлён | `journey_complete`, `fd245814`; полировка до `a096e204` |
| LevelUpModal | Старый | Действующий хвост № 3 |
| DailyQuestRewardModal | Старый | Действующий хвост № 4 |
| IntroScreen из ScreenEnum | Старый шаблон; активный маршрут не подтверждён | В текущем игровом маршруте сюжетное интро строит `Intro.cs`. В очередь входит именно живая оболочка |
| Bootstrap / Loading, вне ScreenEnum | Обновлён | `background_loading.png`, `ea619af7`; рисованный shared progress, `b9ea151f` |
| Tutorial, вне ScreenEnum | Старый | Действующий хвост № 2 |
| Живое сюжетное Intro, вне ScreenEnum | Старый UI вокруг иллюстраций | Действующий хвост № 5 |
| No Network, вне ScreenEnum | Старый | Действующий хвост № 6 |

**Что меняли за неделю**

| Дата | Основные изменения |
|---|---|
| 30.08 | Home `7c48c321`, League `4f50f491`, Quests `82e45287`, Hero `54cde579` / `8422ac7e`, игровой HUD `c59e9164`, Skills `716a059a` |
| 01.09 | Select Level `f063371c`, Shop `73a86c33`, Settings + Account Prompt `3588ddc9`, Pause/Win/Lose `77988aa9`, Journey Complete `fd245814` |
| 03.09 | Loading `ea619af7`, shared progress `b9ea151f`; размеры, табы, прокрутка, HUD, заголовки, локализация Journey Complete |

Коммит `0ade9e86` от 03.09 укреплял разрешение конфликта сохранений: менялся C# аккаунта и cloud sync. UXML, USS и художественные ассеты окна он не обновлял.

Представление CloudSaveConflict последний раз менялось 07.08; LevelUp — 18.08; DailyQuestReward — 06–07.08. Эти окна остались за пределами основной волны редизайна.

**Что показал WorkOnScreens**

- Эталон: [Settings v07][settings-preview], правила стиля — [Visual_UI_Style.md][visual-style]. Приглушённая живописная фактура, чёрный контур, рисованные панели и контролы, выразительные заголовки.
- Prepared содержит 14 папок: Cloud Save Conflict, Hero, Home, In-game HUD, Journey Complete, League, Quests, Select Levels, Settings, Shared, Shop, Skills, Tutorial, Win Fail Pause Modals.
- Cloud Save Conflict: все 7 PNG отсутствуют среди Unity PNG по SHA256; действующие стили также не используют этот набор. [Manifest][cloud-manifest] полезен для native-текста и геометрии, но требует сверки с текущими файлами.
- Tutorial: из 15 PNG совпал только фон, уже лежащий в `sprites/modals/background_modal_new_york_full.png`. UI обучения продолжает строиться прежним кодом.
- Account Prompt переиспользует Settings. Loading имеет отдельный макет и уже подключённый фон. Отсутствие их отдельных папок в Prepared не является пробелом.

**Редкие состояния, требующие отдельной трактовки**

- У Tutorial есть код и рисунок завершения, но текущий flow после сценария или Skip сразу запускает первый игровой уровень. Внешних вызовов `TutorialGameplayController.ShowCompletion` не найдено. Это подготовленный резервный экран, его возврат в сценарий — отдельное продуктовое решение. [Текущий flow][tutorial-flow].
- `License Expired` существует в коде, но запрос времени закрыт условием `DateTime.Now < 2025-09-01`. При нормальной дате аудита этот путь не выполняется. Активным визуальным хвостом считается No Network.
- Старый общий `Modal.uxml` — оболочка для модалок. Новые игровые модалки и Account Prompt переопределяют её представление. Сам файл не означает, что все модалки остались старыми.
- Отдельные базовые/тестовые UXML не считались самостоятельными экранами без подтверждённого маршрута. Проверка явных asset-ссылок нашла отсутствующую картинку только в `test.uxml`.

**Проверка после будущей интеграции**

Сначала Cloud Save Conflict и активное обучение: ассеты уже есть. Затем два окна наград. После них — интро, сообщение сети и Retry рейтинга.

При приёмке проверить обе версии сохранения и состояние выбора; все шаги Tutorial и Skip; рост уровня после результата; доступную общую Daily-награду; сюжетное интро; потерю сети в меню; ошибку рейтинга с Retry. Сверить русский/английский текст и раскладку на целевом телефоне.

**Состояние рабочего дерева**

Исходники игры и WorkOnScreens этим аудитом не изменены. Создан этот отчёт; в базе опыта добавлен порядок проверки покрытия UI. Unity, Play Mode, сборки и тесты не запускались.

До аудита уже существовали изменения `GameScreen.uxml`, `shared-hud-controls.uss`, новые `shared/button_pause.png` / `.meta`, а также изменения `docs/experience/error_diagnosis.md` и `docs/experience/tool_usage.md`. Они сохранены. Кнопка Pause в рабочем дереве уже подключена к новой картинке; в HEAD ещё стоит прежняя кнопка.

[cloud-call]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/UI/Modals/CloudSaveConflict/CloudSaveConflictCoordinator.cs:90
[cloud-style]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Content/ui/styles/CloudSaveConflictModal.uss:38
[cloud-preview]: C:/Personal/ChatGpt/WorkOnScreens/Cloud_Save_Conflict/v01_cloud_save_conflict.png
[cloud-pack]: <C:/Personal/ChatGpt/WorkOnScreens/Prepared for Unity Integration/Cloud Save Conflict>
[cloud-manifest]: C:/Personal/ChatGpt/WorkOnScreens/Cloud_Save_Conflict/archive/v01_cloud_save_conflict_work/unity_preparation_technical_archive/cloud_save_conflict_assets_manifest.json:165
[tutorial-view]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/Tutorial/Gameplay/TutorialGameplayView.cs:322
[runtime-style]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Content/ui/styles/components/runtime-states.uss:59
[tutorial-pack]: <C:/Personal/ChatGpt/WorkOnScreens/Prepared for Unity Integration/Tutorial>
[tutorial-qa]: C:/Personal/ChatGpt/WorkOnScreens/_tutorial_unity_work/final_qa/automated_report.json:238
[tutorial-flow]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/Tutorial/Runtime/TutorialFlowController.cs:104
[levelup-call]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/GameEngine/Mechanics/LevelResultNavigationCoordinator.cs:51
[levelup-ui]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Content/ui/uxml/LevelUpModal.uxml:4
[levelup-style]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Content/ui/styles/LevelUpModal.uss:41
[daily-call]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/UI/Screens/QuestsScreenController.cs:232
[daily-ui]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Content/ui/uxml/DailyQuestRewardModal.uxml:4
[daily-style]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Content/ui/styles/DailyQuestRewardModal.uss:31
[intro-view]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/System/LevelManagement/Intro.cs:63
[intro-skip]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/System/LevelManagement/Intro.cs:147
[network-call]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/SharedCore/LicenseManager.cs:33
[network-view]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/SharedCore/LicenseManager.cs:76
[network-scene]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scenes/Menu.unity:435
[league-retry-ui]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Content/ui/uxml/LeaderboardScreen.uxml:47
[league-retry-style]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Content/ui/styles/screens/LeaderboardScreen.uss:299
[account-style]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Content/ui/styles/components/account-prompt.uss:35
[settings-preview]: C:/Personal/ChatGpt/WorkOnScreens/Settings/v07_settings_default.png
[visual-style]: C:/Personal/ChatGpt/WorkOnScreens/Visual_UI_Style.md
