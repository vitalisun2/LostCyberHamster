# Первая сессия: задачи и реализация

2026-09-09. Блок 1. Пользователь разрешил реализацию кода и ассетов, исследования, компиляцию и ревью каждой задачи. Ручное управление компьютером исключено. Игровые замеры и визуальная приёмка остаются отдельным этапом.

## Принятые решения

- Complete и Skip сразу дают 150 XP один раз. Complete использует существующее поздравление; Skip — честный текст стартового бонуса. Replay сохраняет исходный профиль и повторно бонус не выдаёт.
- Работаем с новыми профилями. Перед релизом предусмотрен сброс; миграция старых tutorial-наград исключена.
- Текущий баланс: 240 XP за уровень, 10 XP за новую лучшую звезду, 60 XP за Story Claim, 20 XP за Daily Claim, 50 XP за подтверждённый weekly record. Параметры способностей действующие.
- Офлайн-путь: `150 + 3×10 + 60 = 240 XP`. Три разных Morning на одну звезду и Claim дают первое очко развития. Primary Morning также выдаёт 300 монет.
- Level Up показывает сохранённый результат отдельным существующим окном после забега или Claim. Уведомления в забеге — только предварительный квест и пересечение известного рекорда.
- Открытие щита за 1 DP, экипировка, заряд и применение — реальные игровые действия. «Позже» и выбор Forest доступны.
- Отдельные награды за открытие частей суток и городов исключены; эти цели принадлежат квестам.
- Целевой баланс и редизайн блока 2 описаны в [утверждённом brief](research/09_progression_abilities.md) и [его плане](progression_plan.md). Переход выполняется отдельными задачами блока 2.

## Порядок и проверки

Порядок: 1 → 2; параллельно контент 3/7 и источники 9/10; затем 4 → 5 → 6/7 → 8 → 11. Общие файлы и локализация интегрированы одним владельцем. Контент, источники и UI проверены исполнителями и независимыми ревью.

Для C# финальный gate: актуальный Unity import, `regenerate_project_files`, `dotnet build Assembly-CSharp.csproj --no-restore`, затем `dotnet build Assembly-CSharp-Editor.csproj --no-restore`. Это проверка всей интеграции. Фактическое игровое время, удобство управления и читаемость на телефоне компиляция не доказывает.

Пути реализации ниже относительно `LostCyberHamster/Assets/`. Изменения остаются в `integration/unity-live`; commit, публикация и APK в этот запрос не входят.

## 1. Coin-pickup

**Реализовано:** `CollectCoinsOrBonusAction.CollectCoin()` публикует `CoinCollected(1)`. Crystal-путь сохранён.

**Файл:** `Scripts/Gameplay/CollectCoinsOrBonusAction.cs`.

**Ревью:** сверены обработчики валюты и квестовые события. Один подбор использует один правильный тип события.

## 2. Однократные 150 XP

**Реализовано:** `PlayerExperienceService.GrantExperienceForTutorialCompletion()` начисляет бонус восстановленной копии профиля. `HasReceivedTutorialExperience`, XP/DP и исход Complete/Skip входят в единый completion intent. Intent сначала записывается в tutorial backup, затем сохраняется основной профиль; после очистки backup публикуются события.

`TutorialSession` сохраняет pending Level Up через Retry. Cold recovery восстанавливает intent и публикует состояние уровня без новой выдачи. После снятия backup возобновляются отложенные weekly-выплаты. В существующем финальном окне показан стартовый бонус; для Skip отдельные заголовок и описание.

**Файлы:** `Scripts/GameManagement/PlayerProgress/PlayerData.cs`, `PlayerExperienceService.cs`; `Scripts/Tutorial/Session/TutorialSession.cs`, `Runtime/TutorialFlowController.cs`; RU/EN; `Content/ui/styles/screens/Tutorial.uss`.

**Ревью:** Complete/Skip/Replay, повтор callback, сбой commit и очистки backup, recovery, late weekly во временном учебном профиле. Награда принадлежит сервису; окно её повторно не начисляет.

## 3. Три Morning

**Реализовано:** 19 локальных шаблонов; изменены Morning L1–L3 и старт Afternoon L1. Исходные 160 шаблонов сохранены семантически. У Afternoon остальные семь ссылок прежние.

| Статический показатель | Morning L1 | L2 | L3 |
|---|---:|---:|---:|
| Span центров и межпаттерновые зазоры, units | 310 | 574 | 598 |
| Опасные объекты | 23 | 56 | 65 |
| Еда | 6 | 18 | 16 |
| Монеты | 6 | 6 | 9 |
| Кристаллы | 0 | 0 | 1 |
| Крыши | 1 | 6 | 7 |

L1 — расчётный кандидат около 90 секунд: 81,58 секунды по span при скорости 3,8, плюс реальные ширины, вход и выход. Это расчёт, игровой замер ещё нужен. L2/L3 сохранены по длине с увеличением span на 30,2/31,6 units. Roof-связки сохраняют относительные смещения предметов.

**Файлы:** `Content/locations/level_design_templates/levels/PatternsCollection.json`; четыре `level_XX.json` под `01_New_York/levels/Morning` и `Afternoon/level_01`.

**Ревью:** JSON, уникальность IDs, ссылки, overrides, исходные шаблоны, парность roof+box. Статическая модель энергии с лишним прыжком каждые 12 секунд и одним пропущенным road-food даёт минимум около 53. Она не доказывает реальную доступность подборов.

**Игровая приёмка:** L1 `90±3 сек` движения; L2/L3 не короче исходных; достижимые крыши, еда и окна реакции. Замер учитывать через реальный spawner/коллайдеры, отдельно от пауз и фонового режима.

## 4. Компактные уведомления

**Реализовано:** `NotificationMessage`, `NotificationCoordinator`, `NotificationView`, `FirstSessionNotificationHost`.

Один активный показ, до пяти сообщений вместе с ожидающими. Gameplay: интервал начал 12 секунд, максимум три за минуту; предварительное сообщение живёт до 10 секунд. Quest-события объединяются за одну секунду, подтверждения одной доски/недели — общей сводкой. Непомещающееся сообщение возвращается в очередь с задержкой; остальные получают слот.

Плашка пропускает ввод, учитывает safe area и реальные HUD bounds. Время чтения останавливается при блокировке и фоне. При ошибке записи ACK подтверждённый факт остаётся у владельца и приходит повторно. Настройка в Settings управляет плашками во время забега.

**Арбитраж:** конфликт профиля и текущий диалог; результат/Claim; Level Up; активный урок; подтверждённая информация; preview. Account prompt ждёт приоритетные окна и урок. `AccountPromptCoordinator.Tick()` и `CloudSaveConflictCoordinator.Tick()` повторяют отложенные показы; после await проверяется фактическая модалка.

**Ревью:** bounded queue, TTL, backoff, дедупликация, ACK только после чтения, отсутствие перехвата ввода, pause/focus, асинхронное отключение Game UI.

## 5. Level Up и выбранный маршрут

**Реализовано:** `PlayerLevelPresentation` выводит диапазон между `LastAcknowledgedPlayerLevel` и сохранённым `PlayerLevel`. Новый профиль начинает с курсора 1. Earned DP берутся из диапазона уровней; свободные очки не подменяют награду.

`ShowAsync(continued, openShield, prepareContinuation)` вызывает подготовку маршрута внутри транзакции ACK, до закрытия окна. Подготовка возвращает действие навигации без повторного сохранения. Повтор нажатия не повторяет переход; смена профиля и устаревший async callback отсечены.

`LevelResultNavigationCoordinator.Continue()` обслуживает Next, Restart, Home, Leaderboard и цель. До показа сохраняет направление. `FirstSessionNavigation` хранит адрес/экран/контекст рейтинга; флаги `FirstSessionReturnFromLevelUp` и `FirstSessionReturnToShield` различают ожидание поздравления и выбранный урок. Cold menu восстанавливает нужное продолжение.

Если окно не загрузилось, исходный результат восстанавливается для повторного действия. Pending и маршрут сохраняются. Все Level Up используют окно, включая следующие повышения. Если XP пришёл во время показа, ACK ограничен показанным уровнем.

**Файлы:** `Scripts/UI/Notifications/PlayerLevelPresentation.cs`, `UI/Common/FirstSessionNavigation.cs`, `UI/Modals/LevelUpModalController.cs`; result navigation/mechanics; `MenuEntryPoint.cs`, `GameUi.cs`; LevelUp UXML/USS.

**Ревью:** сохранение ACK+route, холодный вход, ошибка загрузки/commit, выбранный рейтинг, GoalShield, ранний и поздний XP, lifecycle и повторные кнопки. UI не выдаёт XP/DP.

## 6. Открытие и экипировка щита

**Реализовано:** `ShieldOnboardingController`, `ShieldTutorialProgress`, `FirstSessionCoachView`, `FirstSessionFocusOutline`; точечные hooks в `CharacterDevelopmentScreenController` и `CharacterScreenController`.

Урок ведёт по существующим Skills → Energy Shield → открытие за 1 DP → экипировка → Hero → вкладка способностей → щит → «НАДЕТЬ». Фокус накладывается отдельной обводкой. Действия выполняют прежние `CharacterDevelopmentService` и `SuperAttackService`.

Этап вычисляется по unlocked/equipped/used. «Позже» сохраняет свободный маршрут; при ошибке сохранения подсказка остаётся доступной. Уже открытый щит не покупается повторно. Выбор Forest оставляет цель получить следующее очко.

**Ревью:** одна трата, настоящая экипировка, возврат, zero-DP, повтор callback, смена профиля, core snapshot. Длительность и награды читаются из действующих источников.

## 7. Заряд и практика щита

**Реализовано:** `ShieldPracticeController` в настоящем забеге. Пять стартовых smallAlive дают безопасные возможности напрыгивания; обход сохраняется. Реальная шкала получает +20 за уничтожение. При 100 подсвечивается кнопка способности; `UltaActivated` означает готовность, `UltaUsed` подтверждает применение.

Только успешное использование надетого щита сохраняет started+used. Факт привязан к профилю, generation, уровню и живому Hamster. Сбой записи повторяется в этой попытке. Заряд остаётся состоянием попытки; revive её продолжает. Длительность читается из каталога способности, откуда её получает runtime factory.

**Ревью:** без бесплатной способности/заряда, без snapshot обычного уровня, без награды за плашку. Telemetry защиты записывается после фактического контакта при активной защите. Измерение проходимости остаётся игровой приёмкой.

## 8. Ближайшая цель и Claim

**Реализовано:** `FirstSessionGoalPresenter.GetCurrent()` читает существующий progress, Story, DP и экипировку. Цель доступна в Home/выборе уровня и Win.

После Morning L3 большая кнопка Win показывает «Забрать в заданиях» и открывает Story; рядом остаётся «Следующий уровень». Сам Claim — отдельное нажатие в Quests. Первая Story открывается на своей странице, карточка и Claim выделены. Награда берётся из quest/service. После Claim цель ведёт к щиту и первому Afternoon.

Daily поясняет зачёт действий после победы и ручное получение награды. Действующий генератор Daily сохранён. Специальный стартовый Daily-набор остаётся отдельной гипотезой после замеров.

**Файлы:** presenter, Win/Quests controllers, `UiWinModalMechanics`, `MenuNavigationRequest`, Win/Quests UXML и Quests USS.

**Ревью:** 1/3–3/3 считаются по разным уровням, возврат в нужную вкладку, повторный Claim через прежнюю транзакцию, подпись primary соответствует действию, геометрия существующих кнопок сохранена.

## 9. Preview квеста

**Реализовано:** неизменяемые `QuestAttemptPreview`, `QuestAttemptPreviewSnapshot`, evaluator и события `QuestManager.AttemptPreviewChanged/AttemptQuestConditionReached`.

Проекция использует production ActionCounter strategy над committed progress и буфером попытки. Прогресс/IsCompleted/Claim не меняются. UI показывает «Победи, чтобы засчитать». Проверяется profile, generation, attempt, instance, target; новая попытка снимает старый preview, revive сохраняет текущую. Окончательный прогресс сохраняет прежний Win-путь.

**Ревью:** независимое чтение, отсутствие выдач, уникальный instance, rollback/rebind, проигрыш, поздний callback и дедупликация. Победа и Claim остаются разными фактами.

## 10. Preview и подтверждение рекорда

**Реализовано:** immutable baseline в `WeeklyRunContext`, `WeeklyRecordPreview`, score crossing event, сохранённый cache и отдельный presentation ACK в weekly journal.

Preview сравнивает score с известным результатом текущей недели и scope. Неизвестная база не объявляется побитой. Пересечение одно за попытку. Подтверждение и XP принадлежат existing weekly coordinator; tutorial backup временно откладывает выплаты.

`GetPendingRecordNotifications()` — read-only; `AcknowledgeRecordNotification()` отмечает просмотр отдельно от applied XP ledger. Win подтверждает только действительно видимую надпись нового рекорда своего run при явном выходе. Кнопка рейтинга сама просмотром рекорда не считается. Поздние подтверждения ждут меню и полного времени чтения.

**Ревью:** known/unknown, owner/profile/environment/board/version/run, revive, повтор server callback, cold pending, expiry, backup и раздельные reward/ACK. Число XP в UI читается из общего сервиса текущего баланса.

## 11. Диагностика, воронка, связанная приёмка

**Реализовано:** `FirstSessionTelemetry.Record(phase, detail, value)`, отдельное событие UGS `first_session`; runtime hooks tutorial, attempt, DP, Level Up, shield, quests, records и notifications. События tutorial проходят отдельным путём; обычная tutorial-аналитика остаётся подавленной. Production учитывает consent. Editor/Development/test-level дают ECO-диагностику.

Общий `ExperienceProgressTestRunner` предоставляет однократный tutorial-бонус и read-only снимок первой сессии. DEV и Tools/Testing вызывают одинаковые методы. Снимок содержит XP/DP, бонус/outcome, pending Level Up, щит/возврат, weekly baseline и подтверждения. Команда бонуса использует production XP-service и транзакцию.

**Схема UGS:** `fs_schema`, `fs_phase`, `fs_detail`, `fs_value`, `fs_session`, `fs_sequence`, `fs_elapsed_seconds`, `fs_profile`, `fs_level_address`, `fs_player_level`, `fs_tutorial_outcome`, `fs_app_version`, `fs_cohort`, `fs_network_mode`. Приём custom event в UGS Dashboard требует конфигурации этой схемы; серверный приём здесь не проверен.

**Итоговая приёмка кода:** scoped review каждой задачи; независимое ревью награды, навигации, очереди, источников и пяти JSON; RU/EN пары/плейсхолдеры; UXML; Unity metadata; runtime и Editor C# gate. Точные результаты записаны в [отчёте реализации](first_session_implementation.md).

**Отдельная игровая приёмка:** свежий офлайн-профиль, Complete/Skip, три Morning по одной звезде, Claim, щит, первый Afternoon; ранний DP, Forest-first, проигрыш/revive, поздний weekly, прерывания и узкий landscape. Ручное управление компьютером для неё в этом запросе исключено. D1/D7 и фактические 90 секунд остаются измерениями, а не результатами компиляции.

## Источники

[Справочник экономики](README.md), [исходный аудит первой сессии](research/08_first_session.md), [факты Morning](research/08_morning_level_facts.md), [цели](goals.md), [определения удержания](research/06_retention_targets.md), [Unity UI integration](../approaches/unity_ui_asset_integration.md).

Исследование Skip: [официальный FAQ MTG Arena](https://magic.wizards.com/en/mtgarena/getting-started) сохраняет tutorial-награды при Skip; [Hearthstone 28.2](https://hearthstone.blizzard.com/en-us/news/24008697) описывает потерю части наград при пропуске последующего Apprentice Track. Единого правила нет; для этого проекта принята одинаковая стартовая выдача.

Плашки и управление вниманием: [Material snackbars/toasts](https://m1.material.io/components/snackbars-toasts.html), [Xbox Accessibility Guideline 117](https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/117). Длительности и размещение — текущий кандидат для последующей игровой проверки.
