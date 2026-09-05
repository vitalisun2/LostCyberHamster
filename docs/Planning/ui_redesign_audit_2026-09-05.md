# Аудит редизайна UI — 05.09.2026

**Cloud Save Conflict, Tutorial, LevelUp и DailyQuestReward реализованы.** Финальные code/visual review и runtime-проверки — PASS. Открыт последний gate: regeneration и C# build.

За пределами четырёх зон остаются найденные аудитом оболочка сюжетного интро, сообщение «Нет сети» и Retry рейтинга. Account Prompt уже обновлён вместе с Settings.

Первичный аудит: история `integration/unity-live` за 29.08–05.09.2026, UXML/USS/C#, маршруты, сцены, `Content/ui/sprites`, WorkOnScreens; срез `a096e204`, около 14:10 МСК. Дополнение учитывает последующую реализацию и проверки интегратора. Точный scope — [отчёт реализации][implementation].

## Четыре реализованные зоны

| Зона | Подтверждено | Остаток приёмки |
|---|---|---|
| CloudSaveConflictModal | Рисованные панели/карточки/кнопки; 8 кадров RU/EN ×4 размера. Выбор, busy/rebind, nullable Cloud, resize и safe area PASS. [RU][cloud-final-ru], [EN][cloud-final-en]. | Общий финальный gate. |
| Tutorial HUD и Complete | [HUD: 64 комбинации][hud-matrix], [Completion: 16 preview][complete-matrix], overflow 0. Независимые source/visual review PASS. [Runtime][completion-runtime]: сохранение до Play, rebind, одна загрузка первого уровня. | Финальный C# gate. |
| LevelUpModal | Рисованная панель, новая эмблема, общая CTA. 8 кадров RU/EN ×4 размера; [RU][levelup-final-ru], [EN][levelup-final-en]. 3 вызова обработчика: close 1, callback 1. | Общий финальный gate. |
| DailyQuestRewardModal | 8 финальных кадров RU/EN ×4 размера; [RU][daily-preview], [EN][daily-final-en]. Заголовок: top 207, height/measuredHeight 128. [Runtime][daily-runtime]: две FIFO-награды, повторный вызов и отказ без лишних начислений. | Финальный C# gate. |

Пользователь прошёл все 8 шагов Tutorial и остановил Play Mode. Интегратор подтвердил `IsTutorialCompleted=true` и восстановление баланса 731. [Runtime-запись][completion-runtime] дополнительно подтверждает сохранение до Play, одну кнопку, удержание ввода до перехода, rebind и единственную загрузку сцены. Завершение сохраняется до success UI; Play запускает первый уровень без повторной записи. Ошибка сохранения показывает отдельное сообщение и Retry.

## Осталось за пределами выбранных зон

| Экран / состояние | Основание первичного аудита | Следующий шаг |
|---|---|---|
| Оболочка сюжетного интро уровня | Живой `Intro.cs` использует `intro-surface` и обычный Skip. [Оболочка][intro-view], [Skip][intro-skip]. | Оформить оболочку и Skip; сюжетные картинки остаются отдельным контентом. |
| «Нет сети» — No Network | `LicenseManager` добавляет подложку и два английских Label. [Триггер][network-call], [вид][network-view]. | Рисованные общие компоненты и локализация. |
| Retry рейтинга | В League у ошибки загрузки есть обычная кнопка Retry. [Шаблон][league-retry-ui], [стили][league-retry-style]. | Рисованная основа при сохранении сценария повтора. |

**Полная сверка экранов**

Статус «обновлён» означает: новый художественный слой подключён в текущих исходниках. Native-текст, обычные контейнеры и отсутствие отдельной папки ассетов сами по себе не означают пропущенный редизайн.

| Экран / семейство | Статус | Основание |
|---|---|---|
| HomeScreen | Обновлён | `home` + shared HUD, `7c48c321` |
| CharacterScreen — Hero, skins/abilities | Обновлён | `hero`, `54cde579` + интеграция `8422ac7e` |
| CharacterDevelopmentScreen — Skills | Обновлён | `skills`, `716a059a`; дальнейшая полировка карусели и прогресса |
| QuestsScreen — daily/story | Обновлён | `quests`, `82e45287`; общая награда также реализована и проверена |
| SelectLevelScreen — выбор времени суток и уровня | Обновлён | Обе композиции используют `select_level`, `f063371c` |
| LeaderboardScreen — League | Обновлён, остался Retry | `league`, `4f50f491` |
| ShopScreen | Обновлён | Полноэкранный магазин, `73a86c33`; прежний ShopModal удалён |
| SettingsScreen | Обновлён | `settings`, `3588ddc9`; включая язык, гостевой аккаунт и редактирование имени |
| AccountPromptModal | Обновлён | Тот же `3588ddc9`; рисованные панели и кнопки Settings, [актуальные стили][account-style] |
| CloudSaveConflictModal | Обновлён | Рисованный пакет подключён, этап 2 PASS |
| GameScreen — HUD | Обновлён | `in_game_hud`, `c59e9164`; Pause использует shared PNG |
| PauseModal | Обновлён | `modals/pause`, `77988aa9` |
| WinModal | Обновлён | `modals/win`, `77988aa9` |
| LoseModal | Обновлён | `modals/fail`, `77988aa9` |
| JourneyCompleteModal | Обновлён | `journey_complete`, `fd245814`; полировка до `a096e204` |
| LevelUpModal | Обновлён | 8 captures и защита callback подтверждены; общий gate идёт |
| DailyQuestRewardModal | Обновлён | Visual/runtime PASS; финальный C# gate идёт |
| IntroScreen из ScreenEnum | Старый шаблон; активный маршрут не подтверждён | В текущем игровом маршруте сюжетное интро строит `Intro.cs`. В очередь входит именно живая оболочка |
| Bootstrap / Loading, вне ScreenEnum | Обновлён | `background_loading.png`, `ea619af7`; рисованный shared progress, `b9ea151f` |
| Tutorial, вне ScreenEnum | Обновлён | HUD/Complete, сохранение до Play и перезапуск Editor PASS; C# gate PASS |
| Живое сюжетное Intro, вне ScreenEnum | Старый UI вокруг иллюстраций | За рамками четырёх зон |
| No Network, вне ScreenEnum | Старый | За рамками четырёх зон |

**Что меняли за неделю**

| Дата | Основные изменения |
|---|---|
| 30.08 | Home `7c48c321`, League `4f50f491`, Quests `82e45287`, Hero `54cde579` / `8422ac7e`, игровой HUD `c59e9164`, Skills `716a059a` |
| 01.09 | Select Level `f063371c`, Shop `73a86c33`, Settings + Account Prompt `3588ddc9`, Pause/Win/Lose `77988aa9`, Journey Complete `fd245814` |
| 03.09 | Loading `ea619af7`, shared progress `b9ea151f`; размеры, табы, прокрутка, HUD, заголовки, локализация Journey Complete |

Коммит `0ade9e86` от 03.09 укреплял разрешение конфликта сохранений: менялся C# аккаунта и cloud sync. UXML, USS и художественные ассеты окна он не обновлял.

До текущей реализации представление CloudSaveConflict менялось 07.08, LevelUp — 18.08, DailyQuestReward — 06–07.08. Поэтому первичный аудит выделил их отдельно от основной волны.

## WorkOnScreens и редкие состояния

- Эталон: [Settings v07][settings-preview], [правила стиля][visual-style]. Приглушённая живописная фактура, чёрный контур, рисованные панели и контролы.
- Cloud: импортированы 6 игровых слоёв; состав сверён с актуальными panel/divider и native-текстом.
- Tutorial: импортированы 10 игровых слоёв. Фон завершения переиспользуется из `sprites/modals/background_modal_new_york_full.png`; текст нативный.
- LevelUp/Daily: созданы 2 эмблемы 256×256 и общая CTA 392×118. [Manifest и alpha/9-slice QA][reward-manifest]; [LevelUp ref][levelup-ref], [Daily ref][daily-ref].
- Tutorial Complete возвращён в активный маршрут по решению пользователя. [Flow][tutorial-flow] сохраняет завершение до одной Play; Skip сохраняет его и сразу запускает уровень.
- Account Prompt переиспользует Settings. Loading имеет подключённый фон; отсутствие отдельной папки Prepared не означает пробел.
- `License Expired` в исходном аудите ограничен условием `DateTime.Now < 2025-09-01`; активным хвостом считался No Network.
- Общий `Modal.uxml` переопределяется рисованными модалками. Базовые/тестовые UXML учитываются только при подтверждённом маршруте.

## Остаток проверки

Regeneration + build Assembly-CSharp `--no-restore`: PASS, 0 ошибок, 21 предупреждение. Финальные code/visual review и перечисленные проверки четырёх зон подтверждены. Исправления чтения сохранения и teardown Tutorial повторно проверены; подробности и границы QA — в отчёте реализации.

Следующая самостоятельная очередь редизайна: живая оболочка интро, сообщение сети и Retry рейтинга. Для неё нужны проверки русского/английского текста и целевого телефона.

Первичный аудит был read-only. Последующая реализация меняет согласованный scope игры и ассетов; владение записано в [отчёте реализации][implementation]. Это обновление документации затрагивает только два отчёта.

[cloud-call]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/UI/Modals/CloudSaveConflict/CloudSaveConflictCoordinator.cs
[cloud-style]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Content/ui/styles/CloudSaveConflictModal.uss
[cloud-preview]: C:/Personal/ChatGpt/WorkOnScreens/Cloud_Save_Conflict/v01_cloud_save_conflict.png
[cloud-pack]: <C:/Personal/ChatGpt/WorkOnScreens/Prepared for Unity Integration/Cloud Save Conflict>
[cloud-manifest]: C:/Personal/ChatGpt/WorkOnScreens/Cloud_Save_Conflict/archive/v01_cloud_save_conflict_work/unity_preparation_technical_archive/cloud_save_conflict_assets_manifest.json
[tutorial-view]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/Tutorial/Gameplay/TutorialGameplayView.cs
[runtime-style]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Content/ui/styles/components/runtime-states.uss
[tutorial-pack]: <C:/Personal/ChatGpt/WorkOnScreens/Prepared for Unity Integration/Tutorial>
[tutorial-qa]: C:/Personal/ChatGpt/WorkOnScreens/_tutorial_unity_work/final_qa/automated_report.json
[tutorial-flow]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/Tutorial/Runtime/TutorialFlowController.cs
[levelup-call]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/GameEngine/Mechanics/LevelResultNavigationCoordinator.cs
[levelup-ui]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Content/ui/uxml/LevelUpModal.uxml
[levelup-style]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Content/ui/styles/LevelUpModal.uss
[daily-call]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/UI/Screens/QuestsScreenController.cs
[daily-ui]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Content/ui/uxml/DailyQuestRewardModal.uxml
[daily-style]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Content/ui/styles/DailyQuestRewardModal.uss
[intro-view]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/System/LevelManagement/Intro.cs
[intro-skip]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/System/LevelManagement/Intro.cs
[network-call]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/SharedCore/LicenseManager.cs
[network-view]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scripts/SharedCore/LicenseManager.cs
[network-scene]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Scenes/Menu.unity
[league-retry-ui]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Content/ui/uxml/LeaderboardScreen.uxml
[league-retry-style]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Content/ui/styles/screens/LeaderboardScreen.uss
[account-style]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/LostCyberHamster/Assets/Content/ui/styles/components/account-prompt.uss
[settings-preview]: C:/Personal/ChatGpt/WorkOnScreens/Settings/v07_settings_default.png
[visual-style]: C:/Personal/ChatGpt/WorkOnScreens/Visual_UI_Style.md

[implementation]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/docs/Planning/ui_four_zones_implementation_2026-09-05.md
[cloud-final-ru]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/.worktrees/ui-four-zones-qa/cloud-ru-1920-final.png
[cloud-final-en]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/.worktrees/ui-four-zones-qa/cloud-en-1440-final.png
[hud-matrix]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/.worktrees/ui-four-zones-qa/tutorial-hud-matrix.json
[complete-matrix]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/.worktrees/ui-four-zones-qa/tutorial-complete-matrix.json
[levelup-final-ru]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/.worktrees/ui-four-zones-qa/levelup-ru-1920.png
[levelup-final-en]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/.worktrees/ui-four-zones-qa/levelup-en-1440.png
[daily-preview]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/.worktrees/ui-four-zones-qa/daily-ru-1920.png
[reward-manifest]: C:/Personal/ChatGpt/WorkOnScreens/Level_Up/archive/v03_level_up_work/reward_assets_manifest.json
[levelup-ref]: C:/Personal/ChatGpt/WorkOnScreens/Level_Up/v03_level_up.png
[daily-ref]: C:/Personal/ChatGpt/WorkOnScreens/Daily_Quest_Reward/v02_daily_quest_reward.png

[completion-runtime]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/.worktrees/ui-four-zones-qa/tutorial-completion-runtime.json
[daily-runtime]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/.worktrees/ui-four-zones-qa/daily-claim-results.json
[daily-final-en]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/.worktrees/ui-four-zones-qa/daily-en-1440.png
