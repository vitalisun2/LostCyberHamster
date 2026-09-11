# Блок 2 — план прогрессии и способностей

2026-09-09. Реализация поручена: P2-00…16 выполняются последовательно в этой задаче. Commit/push разрешены одному финальному интегратору блока 3 после передачи manifest всех блоков. UX research 10–12 и W1 — рабочие решения интегратора, не отдельное утверждение пользователем деталей.

## Текущий статус реализации

- P2-00: передача принята; checkout/свободный lock проверены, lock занят владельцем блока 2. Исходные 961 текстовых файла сохранены в `.worktrees/progression-block2-baseline` для scoped diff.
- P2-01…14: реализованы XP, локальное W1-решение 0/5, Daily snapshot/Claim, уровни способностей, runtime, UI и связь с первой сессией.
- P2-15: общий AbilityProgressTestingRunner встроен в существующие XP/Level Progress DEV и Tools/Testing. Одинаковые команды, отдельный durable DEV-профиль и Restore. Отдельная новая страница/регистрация не потребовалась.
- P2-16: review и лёгкие проверки кода/каталогов выполнены. Итоговый C# gate, точные границы проверки и manifest — [отчёт](progression_implementation.md). По последнему поручению внешний вид и игровое поведение проверяет пользователь; длинные прогоны и сборки исключены.

Игровые/телефонные и визуальные критерии отмечаются после фактического выполнения. Собственный headless Editor используется для импорта и compile gate; ручного управления компьютером нет.

Утверждённая спецификация — [09](research/09_progression_abilities.md). [README](README.md) обновлён по затронутой реализации; [блок 1](first_session_plan.md) — текущая зависимость. Бизнес-контекст: [goals](goals.md), [retention 06](research/06_retention_targets.md), [monetization 07](research/07_monetization_targets.md). Влияние блока на A30/D1 и прибыль требует наблюдений.

UX-кандидаты: [Level Up](research/10_level_up_ui.md), [общая Daily-награда](research/11_daily_reward_ui.md), [HUD способности](research/12_ability_hud_ui.md). Общие аналоги: Subway Surfers, Temple Run 2, Jetpack Joyride. Предпочтение: отдельное Level Up с заработанными/свободными DP; Daily в Quests после трёх Claim; кольцо времени с секундами и отдельный остаток комбинаций скейта.

## Утверждённый результат

- К Barcelona: 40 разных побед, минимум 58 звёзд отдельно в NY и Paris. С tutorial/Skip и Claim первого Morning Story минимум 1442 XP: 6 заработанных DP, уровень игрока 7. Открытия выбирает игрок.
- Порог 240 XP; первая победа 25, новая best-звезда 2; tutorial 150 один раз; первый Morning Story 60; другие Story 20, цели уровня игрока 0; Daily 5 на карточку; общая Daily-награда 30 монет/0 XP; первый подтверждённый weekly-record дня 5 XP.
- Идеальные открытия: уровни кампании 3, 9, 17, 25, 32, 40. Дополнительные Claim/weekly ускоряют путь. На L3 первое очко возможно ещё до Story Claim.
- Шесть открытий и шесть улучшений требуют 12 DP: уровень 13, 2880 XP. Срок достижения уровня 13 по дням/сессиям не задан. Скины после открытия за 1 DP покупаются отдельно: Forest 20, Cowboy 25, Summer 20 кристаллов.
- Щит I/II/III: 3/5/7 с, разрушение II/III, loot 0/25/50%, общий cap 0/2/3. Молния: 75/100/100% исходной дальности, loot 0/20/50%, cap 0/2/3. Скейт: 5/7/10 с общего игрового времени, 1/2/3 полные комбинации; начатая завершается посадкой.
- Заряд 20/35/20, максимум 100; собственное разрушение способности не заряжает её. Базовый дроп: 70% — 3 монеты, 25,5% — до 20 энергии, 1,5% — до 1 жизни, 3% — 1 кристалл. Отдельные milestone-награды отменены; части суток и города принадлежат квестам.

Полная спецификация — 09. Старые числа README и промежуточная арифметика блока 1 её не переопределяют.

## Владение и передача

Проверена ветка `integration/unity-live`, локальный Unity Lead, без worktree. Код блока 1 передан в рабочем дереве; его dirty-файлы остаются у владельца. Первый этап создал этот план и research 10–12; реализация затем разрешена отдельным поручением.

Владелец блока 1: `01a086c2-24d7-7980-b141-38cdedd9c626`, host `local`. Финальная передача 09.09: [отчёт](first_session_implementation.md); перед записью shared-файлов отдельно подтвердить освобождение integration-lock после cleanup владельца. Переданные контракты:

- `PlayerData.LastAcknowledgedPlayerLevel`; `PlayerLevelPresentation.HasPendingLevel/ShowAsync(continued,openShield,Func<bool,Action> prepareContinuation)`; prepare вызывается внутри ack-транзакции и возвращает только навигационный Action. Все Level Up — существующая модалка вне gameplay.
- `LevelResultNavigationCoordinator.Continue(sourceModal,action,returnLevel,returnScreen,location,part,bool startShieldLesson=false)`; `FirstSessionNavigation.SetReturnRoute/PrepareResume/PrepareShield/Begin/Resume/HasReturnRoute` (namespace `LostCyberHamster.UI`). `FirstSessionReturnFromLevelUp` различает отложенный Level Up и tutorial после cold start; `FirstSessionReturnToShield` сохраняет явно выбранный урок через Level Up/cold resume, `PrepareShield` снимает этот флаг. Ошибка показа возвращает исходный result для повтора; scene/profile/generation guards запрещают устаревший переход.
- `WeeklyLeaderboardCoordinator.GetPendingRecordNotifications/AcknowledgeRecordNotification`, `RecordsChanged`; scope owner/profile/environment/board/version/runId. Applied XP и presentation ack разделены.
- `WeeklyRunContext.PersonalBest` — nullable baseline; `PartOfDayScoreMechanics.RecordPreviewed/LatestRecordPreview/LatestRunId`.
- `ShieldOnboardingController(UIManager,root).Tick/Dispose`; `ShieldPracticeController(Hamster,root).Tick(blocked)/IsPresenting`; настоящие unlock/equip/UltaUsed.

Menu обслуживает blocking Cloud/Claim/DailyReward, затем pending Level Up, coach, confirmed toasts и account wait. По финальному отчёту владельца: scoped/независимые review, локализация и ассеты проверены; Runtime/Editor C# — 0 ошибок, 42/17 существующих предупреждений. Реального прохождения, замера 90 секунд и visual QA на устройстве нет; UGS schema `first_session` в Dashboard ещё не настроена. Это результаты и ограничения блока 1, не запуски в рамках этого плана.

Узкое чтение подтвердило: щит сейчас 5 с и destructive; молния использует `effect.WorldRightEdge` и старый дроп 1 за 1–3/2 за 4+; `DefaultFirstJumpTimeout=10` скейта относится только к ожиданию первого прыжка. В `ISuperAttackRuntime` общего snapshot времени нет. HUD показывает заряд; Daily планирует окно через 1000 мс. Это статические факты, не проведённый игровой тест.

## Общий контракт задач

Пути ниже относительно `LostCyberHamster/Assets/`, кроме явно указанных `docs/`. Новый файл помечен «новый». После передачи владелец — исполнитель соответствующего P2-ID; текущий писатель shared до передачи — блок 1.

- Перед правкой и commit: общий `git status`, свой scope, актуальный контракт и Unity Lead lock. Чужие изменения сохраняются. Новая UI-задача: high, закрепление, локальный Lead, [UI-гайд](../approaches/unity_ui_asset_integration.md).
- Перед изменением общего контракта уведомить основной чат `01a085eb-cce0-75e0-ac47-e1e1d283cc92` и владельца блока 1: причина, методы/файлы, новая приёмка. После согласования обновить собственный план. Это правило будущей реализации; текущие документы контрактов кода не меняют.
- UI: утверждённый эталон, подготовленные ассеты, переиспользование shared, числа/текст из кода, alpha/9-slice/safe area, bundle `ui`, один владелец clipping, независимый visual QA. Локализация `Content/localization/lang.ru.json` и `Content/localization/lang.en.json` записывается по очереди. Новые .meta генерирует Unity.
- C# gate по [конвенциям](../../rules/code_conventions.md): scoped review, `regenerate_project_files`, `dotnet build <affected generated .csproj> --no-restore`. Новые unit/EditMode/PlayMode-тесты и editor-only harness не планируются.
- Игровые, телефонные и visual-сценарии ниже — будущая приёмка пользователя либо агента по отдельному явному запросу. Compile не закрывает runtime-критерии. Дополнительные запуски автоматически после C# gate не следуют. Unity automation выполняется последовательно.
- Новая testing-функция появляется одновременно в DEV и `Tools/Testing`, использует общий runner/production service. Проверяются одинаковые состояния, обновление двух view, вход/выход Play Mode. Поверхности — P2-15.
- Награда, баланс и mark сохраняются одной production-транзакцией; события UI публикуются после commit. Показ/ack ресурсы не меняют. Смена profile/generation инвалидирует старые callbacks.
- Итог каждой задачи: свой diff, C# gate, фактическая приёмка, оставшиеся игровые замеры и чужие blockers. README обновляется фактами принятого кода.

## P2-00 — принять контракты блока 1

**Результат документальной сверки 09.09:** финальные контракты приняты как исходные для будущих задач по обновлённым [плану блока 1](first_session_plan.md), [отчёту](first_session_implementation.md) и узкому чтению кода. Проверены `prepareContinuation` внутри ack+route, `FirstSessionReturnToShield`, optional `startShieldLesson`, сброс флага в `PrepareShield`, Result Retry и stale async guards. Контракты использованы в реализации блока 2; итог ниже в отчёте.

**Чужие проверки/ограничения:** владелец сообщил regeneration `c5f5fca1a21c4381bd49c68e5b886bf5`, Runtime 0 errors/42 warnings, Editor 0 errors/17 warnings; Play выключен, типы загружены. Самостоятельного прогона здесь нет. Ручные UI/Play/phone и фактические 90 секунд L1 не проверены; UGS-приём `first_session` требует Dashboard schema. Commit/APK не выполнялись. Текущий баланс остаётся tutorial 150/threshold 240/star 10/Story 60/Daily 20/weekly 50 до задач блока 2.

**Игрок:** новая прогрессия сохраняет tutorial, результаты, маршруты и доставку наград.

**Владение:** интегратор блока 2, этот план. Read-only: `Scripts/UI/Notifications/PlayerLevelPresentation.cs`, `Scripts/GameEngine/Mechanics/LevelResultNavigationCoordinator.cs`, `Scripts/UI/Common/FirstSessionNavigation.cs`, `Scripts/Tutorial/Progress/ShieldOnboardingController.cs`, `Scripts/Tutorial/Progress/ShieldPracticeController.cs`, `Scripts/Leaderboard/WeeklyLeaderboardCoordinator.cs`, `Scripts/UI/Common/UIManager.cs`, `Scripts/GameManagement/PlayerProgress/PlayerData.cs`, `Scripts/UI/Screens/GameScreenController.cs`, план блока 1.

**Порядок:** после финальной передачи блока 1 и освобождения shared-lock. До передачи допустимы исследования и изолированная подготовка ассетов.

**Изменения/состояние:** зафиксировать финальный commit/dirty scope; pending/ack/route API; безопасные точки показа; shield practice; HUD; cloud replacement и tutorial backup. Назначить последовательные writer-окна P2-01…06. Подтвердить aspect ratios, экранные эталоны, место индикатора.

**Приёмка/минимум:** владелец подтвердил готовность; каждый shared-файл имеет одного писателя; API существуют либо заменены в плане финальными именами. Достаточна проверка файлов/статуса. Незакрытые runtime/compile-критерии блока 1 записаны зависимостью; игровой код и сохранения здесь не меняются.

## P2-01 — XP первой победы и новых звёзд

**Игрок:** 27/29/31 XP за первую победу с 1/2/3 звёздами; повтор без нового best даёт 0; каждая новая лучшая звезда даёт 2.

**Владение:** progression writer: `Scripts/GameManagement/PlayerProgress/PlayerExperienceService.cs`, `Scripts/System/LevelManagement/LevelManager.cs`; новый `Scripts/GameManagement/PlayerProgress/ExperienceGrantResult.cs` для фактической квитанции. При необходимости поля в `Scripts/GameManagement/PlayerProgress/PlayerData.cs`, `Scripts/GameManagement/Persistence/PlayerDataValidator.cs` — через интегратора.

**Порядок:** P2-00; первая последовательная правка XP. Tutorial 150 и его одноразовый флаг уже реализует блок 1.

**Изменения:** 25 за переход `LevelProgressKey` из 0 звёзд в ≥1 плюс 2 × прирост best. Старый snapshot читается до записи updated; результат/XP/DP сохраняются одной транзакцией `LevelCompleted`. Ключ включает location/daypart/level. Отдельный first-win ledger нужен только если финальный progress перестанет однозначно хранить первую победу; это фиксирует интегратор.

**Состояние:** порог 240, перенос остатка, +1 DP за повышение. Нулевая награда — валидный исход источника, без вызова текущего `GrantExperience`, отклоняющего ≤0. Receipt содержит фактический XP/source/fromLevel/toLevel; сумма не выводится из изменения остатка. Повтор сохранённого результата даёт 0. Tutorial/test-level сохраняют guards блока 1.

**Приёмка/минимум:** первые 1/2/3 звезды дают 27/29/31; 1 затем 3 дают 27+4=31; 3 затем 1 дают 31+0; Lose/duplicate дают 0. Одинаковый levelId в разных частях имеет разные ключи. 239+27: level+1, остаток 26, +1 DP. Разрыв до/после checkpoint оставляет согласованные best/XP. Review+C# gate; Win/повтор и fault-сценарий P2-15 — отдельная проверка.

## P2-02 — XP квестов по смыслу цели

**Игрок:** 5 XP Daily, 60 за первый NY Morning Story, 20 за другие Story, 0 за цели уровня игрока; валюта сохранена.

**Владение:** quest writer: `Scripts/SharedCore/Meta/Quests/Runtime/QuestManager.cs`, новый `Scripts/SharedCore/Meta/Quests/Runtime/QuestExperienceRewardPolicy.cs`; `Scripts/SharedCore/Meta/Quests/Story/StoryQuestGenerator.cs`, `Scripts/SharedCore/Meta/Quests/Daily/DailyQuestGenerator.cs`, `Scripts/SharedCore/Meta/Quests/Catalog/QuestCatalog.cs`, `Scripts/SharedCore/Meta/Quests/Catalog/QuestCatalogData.cs`, `Content/quests/questData.json`; XP-сервис после P2-01. Отображение XP в `Scripts/UI/Components/QuestItem.cs`, `Scripts/SharedCore/UI/QuestTitleFormatter.cs` — в согласованное UI-окно.

**Порядок:** P2-01; до P2-03/10/11. QuestManager передан блоком 1.

**Изменения/состояние:** одна политика для preview и Claim. Первый NY Morning определяется по Primary и стабильным location/daypart. Все PlayerState/PlayerLevel цели, включая Secondary «достичь 2», дают 0 XP, но 300 монет. Claim сохраняет награду/mark/XP атомарно. Generated/catalog/restored definitions используют одну политику; различия только Daily/Story недостаточно. Два Story-слота и ротация после смены даты сохраняются. Отдельных выплат за открытие города нет по решению 09.

**Приёмка/минимум:** Daily 10 монет+5 XP; Morning Primary 300+60; другой Story 300+20; PlayerLevel 300+0. Нулевой XP не вызывает исключение или цепную выдачу. Повторный/поздний Claim, ротация и перезапуск сохраняют одну выплату/ID квеста. Review+C# gate; профили 239 XP с разными Claim, обе поверхности P2-15.

## P2-03 — устойчивый Claim общей Daily-награды

**Игрок:** забирает 30 монет после трёх Claim, в том числе после возврата и смены даты.

**Владение:** quest writer: `Scripts/SharedCore/Meta/Quests/Runtime/QuestManager.cs`, `Scripts/SharedCore/Meta/Quests/Daily/DailyQuestService.cs`, `Scripts/SharedCore/Meta/Quests/Daily/DailyQuestSetState.cs`; новый `Scripts/SharedCore/Meta/Quests/Daily/DailyCommonRewardSnapshot.cs`. `Scripts/SharedCore/Meta/Quests/Daily/DailyQuestScheduler.cs` — read-only календарь.

**Порядок:** P2-02; до P2-11. Последовательная запись QuestManager.

**Изменения/состояние:** snapshot `(profile,generation,setId,originDate,type,amount)` и Claim ожидаемого SetId. Сейчас Claim без аргумента получает голову очереди; при ротации между показом и нажатием это может оказаться другая награда. Сохранить FIFO `PendingCommonRewards`, суммы старых наборов, `CommonRewardClaimed`, перенос незабранных карточек. Ротация по локальной полуночи и `UsedGenerationDates` прежняя. Presentation-флаг посещения отделён от ledger выплат.

**Приёмка/минимум:** три выполненных задания без Claim не открывают бонус; три Claim дают доступ к 30 монетам/0 XP; повтор — 0. Два старых SetId получают 2×30. Полночь, смена головы очереди или профиля при окне не выдают другую награду. Смешанные старые карточки/новые слоты сохраняют контракт набора. 15 XP относятся к трём свежим Daily; старые Claim учитываются отдельно. Save failure оставляет Retry. Review+C# gate; пограничные состояния P2-15; UI — P2-11.

## P2-04 — weekly: 5 XP за первый подтверждённый рекорд дня

**Игрок:** получает дневные 5 XP за настоящий рекорд; следующие подтверждения показывают рекорд с фактическими 0 XP.

**Владение:** weekly writer: `Scripts/Leaderboard/WeeklyLeaderboardCoordinator.cs`, `Scripts/Leaderboard/WeeklyLeaderboardRun.cs`, `Scripts/Leaderboard/WeeklyLeaderboardJournal.cs`, `Scripts/Leaderboard/WeeklyRecordNotification.cs`; новый `Scripts/Leaderboard/WeeklyDailyRewardDecision.cs`. Shared `Scripts/GameManagement/PlayerProgress/PlayerData.cs`, `Scripts/GameManagement/PlayerProgress/PlayerExperienceService.cs`, `Scripts/GameManagement/Persistence/PlayerDataValidator.cs` — интегратор. `Scripts/Leaderboard/WeeklyScoreMetadata.cs`, `Scripts/Leaderboard/WeeklyRunContext.cs` — существующий контекст; расширять только при необходимости версии receipt.

**Порядок:** после P2-02 и передачи weekly блока 1; в общей writer-очереди после P2-03, до P2-05. Серверное доказательство рекорда сохраняется.

**Рабочий выбор W1 интегратора:** UTC-дата первого durable `ConfirmedImprovement`, не дата забега или показа. `FirstConfirmedAtUtc` записывается один раз. Поздний offline-run занимает день подтверждения. Cloud/tutorial могут отложить применение, сохраняя право исходного дня; разные дни могут выдать больше 5 XP при восстановлении.

**Ограничение W1:** решение обеспечивает единый лимит по доскам локального owner/environment-журнала. Глобальная квота между независимыми устройствами и доверенное серверное время отсутствуют: текущий API подтверждает рекорд, timestamp берётся с клиента. Скачок часов на ранее не использованную дату открывает новую локальную квоту; возврат на уже использованную дату её не повторяет. Интегратор принял это как частичное обеспечение; новый backend вне текущего scope. Проверки retry/restart/cloud rollback/owner и времени фиксируются отдельно в P2-16.

**Ledger:** квота `(owner,environment,UTCdate)` общая по доскам и неделям: reset недели её не удваивает. Receipt сохраняет profile/board/version/runId/confirmedAt/awardedXP 0 или 5/rewardId. Первый зафиксированный рекорд получает устойчивое решение в owner-scoped journal; retry победителя дня не переизбирает. PlayerData применяет rewardId+XP+DP одной транзакцией. После выбора старого cloud восстанавливается тот же rewardId.

**Разделение:** ConfirmedImprovement — серверный факт; решение 0/5 — квота; presentation ack — просмотр. Все обработанные run, включая 0 XP, получают окончательный decision и могут публиковаться. Gate `AppliedWeeklyRewardRunIds.Contains(runId)` в публикации/уведомлениях и фиксированные 50 в диагностике заменить фактическим decision. Нулевые XP не подавляют record acknowledgment. Уже обработанные старой версией run остаются обработанными: прежние 50 XP не пересчитываются и не получают дополнительные 5. Unknown baseline, preview, LocalOnly/Expired/NotImproved сами XP не дают.

**Часы/сохранение:** в прочитанном run timestamp отсутствует; текущие `DateTime.UtcNow` — клиентские. Исполнитель фиксирует источник времени в receipt. Без доверенных серверных часов и серверной квоты смена часов/два устройства остаются ограничением локального лимита. Серверный claim не выдаётся за существующий API. Дневной ledger хранится дольше UI-ack: до безопасной границы cloud/retry.

**Приёмка/минимум:** два рекорда дня на разных досках =5+0; следующий UTC-день =5; равный score =0; первая запись следует текущему доказательству ConfirmedImprovement. Retry/run duplicate дают один decision. Проверить 23:59/00:00, week reset, неактивного owner, cloud rollback, tutorial backup, поздний apply, сообщение с 0 XP, 239+5 = level+1/остаток 4. Review+C# gate; управляемое подтверждение P2-15 отдельно от реального сетевого прогона.

## P2-05 — каталог уровней и покупка улучшений

**Игрок:** открывает способности/скины как прежде; улучшает открытую способность до II/III за 1 DP сразу после открытия, если есть свободный DP.

**Владение:** progression writer: `Scripts/SharedCore/Meta/CharacterDevelopment/CharacterDevelopmentService.cs`, `Scripts/SharedCore/Meta/SuperAttacks/Catalog/SuperAttackData.cs`, `Scripts/SharedCore/Meta/SuperAttacks/Catalog/SuperAttackDataList.cs`; новые `Scripts/SharedCore/Meta/SuperAttacks/Catalog/SuperAttackLevelData.cs`, `Scripts/SharedCore/Meta/SuperAttacks/Catalog/SuperAttackLevelResolver.cs`; `Content/super_attacks/super_attacks.json`, `Scripts/SharedCore/Meta/SuperAttacks/SuperAttackService.cs`. Shared `Scripts/GameManagement/PlayerProgress/PlayerData.cs`, `Scripts/GameManagement/Persistence/PlayerDataValidator.cs`, `Scripts/GameManagement/Persistence/CheckpointReason.cs` — интегратор.

**Порядок:** P2-04; до P2-06/13. UI — отдельная задача.

**Изменения:** ID 1/2/3 получают точные I/II/III из 09. API: tier/cost/prerequisites/CanUpgrade/TryUpgrade(expectedCurrentLevel). I получается через unlock; II требует I, III требует II, DP≥1. Покупка атомарно списывает 1 DP и меняет tier. Повтор с тем же expected tier возвращает актуальное состояние без второй траты. Открытая способность доступна для экипировки без кристаллов.

**Сохранение:** сериализуемый список `(abilityId,level)` в PlayerData; closed=0, открытая способность без нового поля=I. Нормализация согласует unlock/tier, не дарит II/III; неизвестные ID и недопустимые уровни диагностируются. Версии каталога/save раздельны. Cloud/tutorial snapshot/profile replacement переносят поля целиком. Ошибка save откатывает DP/tier. Заряд/эффект остаются состоянием попытки.

**Трактовка порога:** отдельного порога по уровню для ветки улучшений нет. Владение всеми шестью открытиями отдельным условием 09 не задано. Улучшение доступно на любом уровне при открытой способности, корректном предыдущем tier и свободном DP. Выбор и трата ручные.

**Приёмка/минимум:** девять конфигураций совпадают с 09; II/III стоят по 1; DP 0 блокирует, DP 1 разрешает при открытой способности и корректном предыдущем tier; III до II блокирован. Double click/retry/restart дают одну трату. Forest: открыть за 1 DP, купить за 20 кристаллов отдельно; Cowboy 25, Summer 20. Каталог требует 12 DP; на уровне 13/2880 XP после целевых трат свободных DP 0. Review каталога/save+C# gate; обе поверхности P2-15.

## P2-06 — общий runtime времени, разрушения и дропа

**Игрок:** улучшение реально меняет эффект; таймер правдив; одно препятствие не дублирует награду.

**Владение:** runtime-интегратор: `Scripts/SharedCore/Meta/SuperAttacks/ISuperAttackRuntime.cs`, `Scripts/SharedCore/Meta/SuperAttacks/SuperAttackFactory.cs`, `Scripts/SharedCore/Meta/SuperAttacks/UltaMechanics.cs`, `Scripts/SharedCore/Meta/SuperAttacks/UltaChargeMechanics.cs`; новые `Scripts/SharedCore/Meta/SuperAttacks/SuperAttackRuntimeSnapshot.cs`, `Scripts/SharedCore/Meta/SuperAttacks/SuperAttackDropBudget.cs`; `Scripts/Gameplay/Hamster.cs`, `Scripts/GameEngine/Controllers/CollisionController.cs`, `Scripts/GameEngine/Mechanics/UnspawnOnJumpedOnMechanics.cs`, `Scripts/GameEngine/Mechanics/AddCoinsOrBonusMechanics.cs`. `Scripts/GameEngine/Mechanics/ObstacleBonusDropPolicy.cs` — read-only распределение; менять только при доказанной необходимости интерфейса.

**Порядок:** P2-05; до трёх runtime-задач и HUD. Общие файлы пишет один исполнитель; P2-07…09 получают interfaces.

**Изменения:** factory разрешает tier в неизменяемые параметры активации. Runtime отдаёт phase/remaining/duration/combinations/activationId, duration optional для молнии. Обновление принадлежит gameplay lifecycle; HUD читает snapshot. Разрушение различает обычное напрыгивание и ability-source, spawned-instance/generation, факт уничтожения до возврата в pool. Список кандидатов молнии ещё не доказывает уничтожение.

**Дроп:** после реального разрушения щитом/молнией — проверка уникального экземпляра, один roll по p до исчерпания cap; успех резервирует слот и вызывает существующий drop с данными места разрушения. Cap общий для всех ресурсов, сбрасывается на новой activation. Старый дроп молнии заменяется; обычный и ability-drop не суммируются. Ability-source не проходит обычную зарядку. Подбор при полном ресурсе не возвращает drop-slot.

**Состояние/приёмка:** budget/seen/timer живут в attempt/activation; Dispose не переносит их в новый run. p=0 даёт 0 дропов; успешные roll на 10 целях дают максимум 2/3; повтор события экземпляра даёт 0 дополнительных, новое рождение из pool имеет новую идентичность. Монеты 3; состав 70/25,5/1,5/3; обычный заряд 20/35/20, собственный ability-destroy +0. Review полного потока+C# gate; реальный дроп/подбор/коллизии отдельно. Override имеет обе поверхности P2-15 и восстановление обычной policy.

## P2-07 — Energy Shield I/II/III

**Игрок:** I защищает 3 с; II защищает/разрушает 5 с, p25%, cap2; III — 7 с, p50%, cap3.

**Владение:** shield writer: `Scripts/SharedCore/Meta/SuperAttacks/EnergyShieldAttack.cs`; при необходимости визуального завершения `Scripts/SharedCore/Meta/SuperAttacks/Effects/EnergyShieldUlta.cs`. Shared collision/drop меняет интегратор P2-06.

**Порядок:** P2-06; независим от P2-08/09 внутри своих файлов. Урок — P2-14.

**Изменения/состояние:** один scaled gameplay-таймер управляет защитой, destructive flag и snapshot. Сейчас флаги снимает WaitForSeconds отдельно от `_timeLeft`; новая реализация устраняет расхождение действия/HUD. I оставляет препятствие целым; II/III передают успешное разрушение budget-сервису. Finish/dispose/switch освобождают свою защиту, учитывая других владельцев invulnerability.

**Приёмка/минимум:** контакт на 0,1 с, перед expiry, после expiry. I сохраняет жизнь, препятствие целое, дроп/заряд 0. II/III уничтожают один раз, соблюдают p/cap. Пауза останавливает таймер; finish/restart очищают флаги/эффект. Review+C# gate. Фактические 3/5/7 с, коллизии и поведение после прохода сквозь целое препятствие I проверяются живым сценарием.

## P2-08 — Electric Strike I/II/III

**Игрок:** видит дальность 75/100/100%, получает loot 0/20/50% с cap0/2/3; линия удара прежняя.

**Владение:** electric writer: `Scripts/SharedCore/Meta/SuperAttacks/ElectricStrikeAttack.cs`, `Scripts/SharedCore/Meta/SuperAttacks/Effects/ElectricStrikeUlta.cs`. При доказанной необходимости prefab: `Content/skins/ultaEffects/ElectricStrikePrefab.prefab`, через Unity CLI после явного запроса; GUID/контракт сохраняются. Сначала проверить runtime-настройку существующего эффекта.

**Порядок:** P2-06; изолирован от 07/09. Factory передан общим интегратором.

**Изменения/состояние:** зафиксировать исходный origin и world-длину эффекта. 0,75 масштабирует длину от origin, не абсолютную координату X. Видимый конец и граница целей берутся из одних параметров. Сохранить same-line/physical-target, reserved roof/pending jump исключения и повторную проверку живой цели. Волна с интервалами 0,1 с — исполнение, не длительный buff. Старый выбор 1/2 дропов заменяется P2-06.

**Приёмка/минимум:** цели на 0,74/0,76/0,99/1,01 базовой длины, другая линия, reserved roof, цель исчезла между кадрами. Видимая граница совпадает с физической на I/II/III. 0 целей — 0 дропа; много целей — общий cap2/3; самозаряд 0; обычные разрушения 35+35+30=100. Review+C# gate; фактический радиус/visual QA отдельно.

## P2-09 — Skateboard: общий таймер и комбинации

**Игрок:** до 5/7/10 с и до 1/2/3 полных комбинаций; начатый цикл заканчивается посадкой.

**Владение:** skate writer: `Scripts/SharedCore/Meta/SuperAttacks/Skateboard/SkateboardAttack.cs`, `Scripts/SharedCore/Meta/SuperAttacks/Skateboard/SkateboardAttackComposer.cs`, `Scripts/SharedCore/Meta/SuperAttacks/Skateboard/SkateboardJumpCycleSnapshot.cs`. При необходимости terminal state: `Scripts/SharedCore/Meta/SuperAttacks/Skateboard/SkateboardInteractionPolicy.cs`, `Scripts/SharedCore/Meta/SuperAttacks/Skateboard/SkateboardLandingImpactRuntime.cs`, `Scripts/SharedCore/Meta/SuperAttacks/Skateboard/SkateboardLandingImpactTimeline.cs`. Общий collision writer — P2-06.

**Порядок:** P2-06; независим от 07/08; HUD после 09.

**Изменения:** 10 с first-jump timeout заменить общим scaled-таймером от успешной активации; отсчёт продолжается после первого прыжка. Budget списывается при начале новой полной комбинации; простой/усиленный/двойной цикл =1. Второй ввод внутри двойного прыжка новую комбинацию не создаёт. Expiry запрещает следующий цикл; принятый доходит до посадки и восстановления surface/actor. В Ride expiry выключает режим сразу; последняя комбинация заканчивается посадкой даже при оставшемся времени.

**Состояние:** ActiveRide/ActiveCycle/FinishingLanding/Inactive; remaining clamp0, budget неотрицателен. HUD показывает новые доступные комбинации. Ещё не начавшийся queued jump на границе expiry проверяет FSM. Бесплатные по энергии прыжки сохраняются. Разрушения/landing waves используют ability-source; нового лута скейту 09 не назначает. Обычные параллельные pickups сохраняются.

**Приёмка/минимум:** без прыжков завершение на 5/7/10 с; первая комбинация на 4,9 с уровня I досаживается после 5 с, следующая недоступна. Простой/усиленный/двойной цикл списывает 1, отклонённый ввод 0. Expiry в Ride/Jump/Landing, road/roof, queued input, pause, damage/revive, finish/dispose. Энергия прыжка: расход 0; собственное разрушение: заряд 0; wave не дублирует дроп. Review полной FSM+C# gate; живые посадки/коллизии отдельно.

## P2-10 — торжественное Level Up

**Игрок:** после результата/Claim видит повышение, заработанные/свободные DP и доступные открытия/улучшения.

**Владение:** Level Up UI writer: `Scripts/UI/Modals/LevelUpModalController.cs`, `Content/ui/uxml/LevelUpModal.uxml`, `Content/ui/styles/LevelUpModal.uss`, `Scripts/UI/Notifications/PlayerLevelPresentation.cs`; новый `Scripts/UI/Notifications/PlayerLevelRewardViewModel.cs`. Shared `Scripts/UI/Common/UIManager.cs`, `Scripts/GameEngine/Mechanics/LevelResultNavigationCoordinator.cs`, `Scripts/UI/Common/FirstSessionNavigation.cs` — интегратор; локализация по очереди. Reward/shared sprites переиспользуются; scope нового арта определяется эталоном.

**Порядок:** P2-00…05 и принятый кандидат [10](research/10_level_up_ui.md). Для полной приёмки CTA нужен P2-13.

**Изменения/состояние:** существующая доставка/ack блока 1 получает новую композицию: диапазон, earned/free DP, реальные возможности, Continue/развитие. Все Level Up — модалка вне run; верхний toast остаётся квесту/рекорду. Награда сохраняется XP-транзакцией, pending/ack — отдельно. Новый XP при открытом окне оставляет pending сверх показанного toLevel. Route использует FirstSessionNavigation; callback одноразовый. Ack и выбранный маршрут сохраняются до закрытия окна через prepareContinuation, по контракту блока 1.

**Приёмка/минимум:** research 10: поздний Claim/weekly, Win Домой/Дальше/Leaderboard/Restart, несколько повышений, новый XP при окне, ошибка ack, cloud/profile, развитие и возврат. Переоткрытие не начисляет XP/DP. RU/EN и safe area, review+C# gate; live visual QA и понятность отдельно.

## P2-11 — усилить существующую Daily-модалку

**Игрок:** видит полученные 3/3, получает 30 монет или возвращается за ними позже.

**Владение:** Daily UI writer: `Scripts/UI/Modals/DailyQuestRewardModalController.cs`, `Scripts/UI/Screens/QuestsScreenController.cs`, `Content/ui/uxml/DailyQuestRewardModal.uxml`, `Content/ui/uxml/QuestsScreen.uxml`, `Content/ui/styles/DailyQuestRewardModal.uss`, `Content/ui/styles/screens/QuestsScreen.uss`; локализация. `Scripts/UI/Common/UIManager.cs` — следующее shared-окно после P2-10.

**Порядок:** P2-03, P2-10, кандидат [11](research/11_daily_reward_ui.md). Выдача остаётся в Quests.

**Изменения/состояние:** плашка полученных карточек/бонуса; trigger после трёх Claim и освобождения очереди; SetId snapshot; Получить/Later; FIFO старых наград с датой/числом оставшихся. Автопоказ один раз на посещение Daily, ручной вход постоянный. Scheduler/callbacks отменяются при смене root/profile. При одновременном Level Up порядок research 10; общий бонус 0 XP.

**Приёмка/минимум:** три выполненных без Claim, третий Claim, Later, полночь при окне, старая очередь, duplicate/save failure, возврат. Выплата только 30 × число полученных SetId, без нового XP; view не подменяет Claim. RU/EN/landscape, review+C# gate; живой поток по запросу.

## P2-12 — активная способность в новом HUD

**Игрок:** различает заряд/готовность/действие, видит секунды щита/скейта и остаток комбинаций.

**Владение:** HUD writer: `Scripts/UI/Screens/GameScreenController.cs`, `Content/ui/uxml/GameScreen.uxml`, `Content/ui/styles/screens/GameScreen.uss`; новый `Scripts/UI/Components/AbilityActivityIndicator.cs`; `Scripts/GameEngine/Mechanics/UiGameScreenMechanics.cs` через интегратора, локализация. Переиспользовать `Content/ui/sprites/in_game_hud`; динамическое кольцо не запекать в PNG.

**Порядок:** P2-06…09, окончательный HUD блока 1, кандидат [12](research/12_ability_hud_ui.md). Один писатель GameScreen/UiGameScreen.

**Изменения/состояние:** snapshot runtime; кольцо/ceil секунд; скейт — новые комбинации/До посадки; молния — короткий отклик. Touch-зоны сохраняются; индикаторы picking Ignore. Pause/background/root/attempt и subscriptions следуют lifecycle. Отдельного persisted-таймера HUD нет. Текущий HUD читает Hamster.UltaChargeAmount и SuperAttackService.Active; новый snapshot добавляется P2-06.

**Приёмка/минимум:** research12: 0 комбинаций в воздухе, expiry с посадкой, молния без таймера, pause/resume, смена activation. Время совпадает с защитой/скейтом; toast/shield focus не перекрывают touch. Review+C# gate; читаемость в движении/visual QA отдельно.

## P2-13 — выбор улучшений в развитии и Hero

**Игрок:** видит tier, следующую выгоду/цену; покупает апгрейд за 1 DP, отличает открытие скина от покупки.

**Владение:** development UI writer: `Scripts/UI/Screens/CharacterDevelopmentScreenController.cs`, `Scripts/UI/Screens/CharacterScreenController.cs`, `Content/ui/uxml/CharacterDevelopmentScreen.uxml`, `Content/ui/uxml/CharacterScreen.uxml`, `Content/ui/styles/screens/CharacterDevelopmentScreen.uss`, `Content/ui/styles/screens/CharacterScreen.uss`; локализация. Общую навигацию меняет интегратор. Новые карточные компоненты — после сверки существующих.

**Порядок:** P2-05 и передача экранов блоком 1. Подготовка независима от runtime; соединение с CTA P2-10 перед приёмкой.

**Изменения/состояние:** I/II/III, текущие/следующие параметры из resolver, цена 1 DP; причина lock: предыдущий tier/DP/открытие; max. Заряд и полный прыжковый цикл следуют 09. Кнопка блокируется до save; успех обновляет Skills/Hero/freeDP/LevelUp-возможности. Экипировка отдельна от покупки. Новые параметры берёт следующая активация; начатая использует свой snapshot.

**Приёмка/минимум:** low/high player level, DP0/1, closed/I/II/III, double click/save failure/reload/cloud; параметры Skills/Hero совпадают. Skin unlock/buy/equip раздельны, цены 20/25/20 реальны. Урок щита находит anchors; Forest-first доступен. Review+C# gate; оба экрана/локализация/live visual QA отдельно.

## P2-14 — переход между блоками и первая сессия

**Игрок:** Complete/Skip дают 150 сразу; очко доставляется при раннем Win/Claim; урок объясняет щит I: 3 с защиты без разрушения. Награда выдаётся один раз, все экраны читают общий источник.

**Владение:** интегратор блоков: `Scripts/Tutorial/Progress/ShieldOnboardingController.cs`, `Scripts/Tutorial/Progress/ShieldPracticeController.cs`, `Scripts/Tutorial/Progress/FirstSessionGoalPresenter.cs`, `Scripts/UI/Common/FirstSessionNavigation.cs`, `Scripts/Gameplay/GameUi.cs`, `Scripts/Entry Points/MenuEntryPoint.cs`, `Scripts/GameEngine/Mechanics/LevelResultNavigationCoordinator.cs`, `Scripts/UI/Common/UIManager.cs`; локализация. Только при доказанном конфликте геометрии и передаче контента: `Content/locations/01_New_York/levels/Morning/level_01/level_01.json`, `Content/locations/01_New_York/levels/Morning/level_02/level_02.json`, `Content/locations/01_New_York/levels/Morning/level_03/level_03.json`, `Content/locations/01_New_York/levels/Afternoon/level_01/level_01.json`, `Content/locations/level_design_templates/levels/PatternsCollection.json`.

**Порядок:** P2-01…13; перед финальной связанной приёмкой. Интерфейсы согласованы заранее, shared-правки последовательны.

**Изменения:** проверить переход со старых 10 XP/звезду, Daily 20, Story 60, weekly 50 и базовых длительностей на 09. P2-01…09 меняют domain; эта задача подключает все отображения/урок. Новая сумма квеста разрешается по конкретному quest через P2-02, а не одной Story-константой. Tutorial/HUD/описание читают эффективный tier через P2-05/06: 3/5/7 с щита, а не только базовое поле каталога. Итоговый Win XP берётся из receipt.

**Состояние:** сохранить tutorial 150/одноразовый mark, реальные unlock/equip, pending/ack/route и подтверждённый Claim. Блок 1 делает большую Morning Win CTA «Забрать в заданиях», отдельная Next продолжает уровень; Claim остаётся только в QuestManager. `FirstSessionReturnFromLevelUp` различает cold pending-route и урок; `FirstSessionReturnToShield`/`startShieldLesson` сохраняют явный выбор урока через pending Level Up, `PrepareShield` снимает флаг. Проверить этот маршрут после cold resume и восстановления Result при ошибке показа; устаревший scene/profile/generation callback не продолжает переход. Ранний L3 Level Up, Forest-first, already equipped, Later и late weekly обслуживаются согласованно. Compile блока 1 не подтверждает контакт/геометрию нового трёхсекундного щита.

**Урок:** строки о 5 с/разрушении заменить эффективным tier. Подтвердить контакт в 3-секундном окне вместо прежних 5 с. Пять обычных разрушений по 20 дают 100 заряда. Для щита I результат практики — защита, не уничтожение. Изменение контента только после доказанного конфликта и согласования владельца.

**Приёмка/минимум:** минимум Morning:150+3×27=231, Claim 60=291 (level 2/51); идеал:150+3×31=243 уже level 2, Claim=303/63. Complete/Skip дают одинаковый бонус, Replay 0. Поздний Claim доставляет окно без победы. Проверить открытие 1 DP, equip, заряд 100, защиту 3 с, выбранный маршрут. При переходе версии сохраняются balances/marks: уже выданные награды не переиздаются показом или повторным Claim; новых legacy tutorial-выплат нет по 09. Три Daily выполнены и три Claim получены остаются разными состояниями. Все четыре адреса practice сохраняют геометрию. Review+C# gate; контакт/проходимость только живым тестом.

## P2-15 — DEV и Tools: диагностика прогрессии и способностей

**Игрок/QA:** повторяемые сценарии; тестовый режим не проникает в обычный профиль.

**Владение:** diagnostics writer:

- `Scripts/DevTools/ExperienceProgressTesting/ExperienceProgressTestRunner.cs`, `Scripts/DevTools/ExperienceProgressTesting/ExperienceProgressTestingView.cs`, `Scripts/DevTools/ExperienceProgressTesting/ExperienceProgressTestingScreen.cs`, `Editor/Testing/ExperienceProgressTesting/ExperienceProgressTestingPage.cs`.
- `Scripts/DevTools/QuestTesting/QuestTestRunner.cs`, `Editor/Testing/QuestTesting/QuestTestingPage.cs`.
- `Scripts/DevTools/SkateboardTesting/SkateboardTestingRunner.cs`, `Editor/Testing/SkateboardTesting/SkateboardTestingPage.cs`, `Scripts/DevTools/Gameplay/GameplayDevToolsScreen.cs`.
- Новые `Scripts/DevTools/AbilityProgressTesting/AbilityProgressTestingRunner.cs`, `Scripts/DevTools/AbilityProgressTesting/AbilityProgressTestingView.cs`, `Scripts/DevTools/AbilityProgressTesting/AbilityProgressTestingScreen.cs`, `Editor/Testing/AbilityProgressTesting/AbilityProgressTestingPage.cs`.
- Регистрация: `Scripts/DevTools/Root/RootDevToolsScreen.cs`, `Editor/Testing/CloudSaveTestingWindow.cs`. Weekly использует существующий сетевой адаптер/общий runner; отдельный editor-only mock harness исключён правилами.

**Порядок:** схема команд в 00; данные после 01…06; полная готовность после 07…13. Каждая функция выпускается в обе поверхности одновременно. Диагностика нужна до запрошенного прогона, но не блокирует обычный C# gate остальных задач.

**Действия/состояние:** изолированный профиль 239 XP/DP/tier; production Claim/upgrade; чтение pending level/route/dailySetId/weekly decision; доступный confirmation-сценарий; remaining/budget/dropcount/charge; допустимый drop override. Каждому изменяющему действию парно Reset/Restore исходного профиля/режима. Состояние читается из production; view не пишет PlayerData напрямую.

**Приёмка/минимум:** одинаковый input DEV/Tools даёт одинаковый receipt/баланс/переход. Одна поверхность обновляет другую без перезахода. Play Mode enter/exit снимает подписки/override; test flags не сохраняются в обычный run. Runtime/Editor получают соответствующие C# gates. Исполнение игровых матриц 01…14 — отдельная разрешённая приёмка.

## P2-16 — принять путь до Barcelona и фактический баланс

**Игрок/продукт:** шесть очков доступны к Barcelona на оговорённом пути; ресурсный поток улучшений проверен.

**Владение:** экономика/интегратор: этот план, `docs/products/economics/README.md`; новый `docs/products/economics/research/13_progression_validation.md`. Если нужны события: `Scripts/Analytics/Events.cs`, `Scripts/Analytics/AnalyticsManager.cs` в отдельное writer-окно; использовать ECO diagnostics. Каталоги читаются; перенастройка 09 автоматически в задачу не входит.

**Порядок:** все 01…15 интегрированы; игровой/телефонный сценарий явно разрешён. Блок 1 сдал coin fix и tutorial bonus.

**Наследуемые ограничения:** около 90 секунд L1 — расчёт блока 1, проходимость и визуальная читаемость на устройстве не измерены. Отчёт о компиляции/ревью не заменяет эту приёмку. Перед выводами по telemetry отдельно подтвердить Dashboard schema и фактический приём UGS `first_session`; сейчас приём событий и retention не подтверждены.

**Статическая приёмка:**

- Каталог подтверждает 40 разных побед до Barcelona и 58 звёзд отдельно в NY/Paris. Минимальный набор:36 побед по 3 звезды и 4 по 2 звезды, по 2 двухзвёздных в каждом городе.
- `150+40×25+116×2+60=1442`: level 7/остаток 2/earned 6 DP. Идеальные 120 звёзд дают 1450/остаток 10. Заработанные DP = свободные плюс реальные траты.
- При 3 звёздах и Claim после L3 суммы 303/489/737/985/1202/1450 на L3/9/17/25/32/40. Первое повышение возможно на 243 до Claim. Без первого Story Claim минимум 1382; гарантия шести DP требует указанного Claim.
- Другие Story/Daily/weekly учитываются отдельно. Два Story-слота не дают бесконечную цепочку в тот же день. Общие Daily 30 монет дают 0 XP. Каталог 12 DP=2880 XP; срок после Barcelona не вычислен.

**Игровая матрица:** свежие Complete/Skip; минимальная звёздность/идеальный путь; ранний/поздний Claim; offline с известным weekly-контекстом/cold-offline; полночь/late confirmation; Forest-first; покупка скинов; все 9 tier на одинаковых адресах; pause/interrupt/cloud; UI-маршруты. Принудительный DEV-progress проверяет математику/сохранение. Проходимость требует игрока или разрешённого реального прогона.

**Ресурсный контроль:** на активацию фиксировать tier, активное игровое время, обычные/ability-разрушения, rolls, созданные/подобранные дропы по типам, cap, charge, start/end энергии/жизней/валюты, комбинации и completion reason. Сравнивать одинаковые участок/скин/исходные ресурсы; override отделить от обычной случайности. Без cap на одно разрушение ожидание =p×2,1 монеты и p×0,03 кристалла. Для n целей и cap C число дропов=`min(Binomial(n,p),C)`; бонус начисляется сразу при roll, анимации не требуют подбора; collectible-подбор учитывается отдельно. Cap одной активации не доказывает доход за run: измерить число активаций, обычных зарядок и покупок ульты. Скейт дополнительно экономит энергию.

**Выход/минимум:** таблица «критерий/профиль/версия/метод/факт/непроверено». Раздельно: статический расчёт, production-backed DEV, живой Editor, Android/игрок. Распределение с числом наблюдений; малая выборка не доказывает точную доходность. README — факты принятого кода. QA не доказывает D1≥35%, D7≥15%, D30≥7%, A30≥5; когортный контроль — по 06. Перенастройка 09 при перекосе — отдельное продуктовое решение.

## Порядок и независимые работы

1. P2-00 после финальной передачи блока 1. Его владелец не получает новый баланс/редизайн автоматически в свой scope.
2. Shared domain одним писателем: P2-01,02,03,04,05,06. XP, PlayerData, QuestManager, weekly, factory/collision одновременно не редактируются.
3. После 06 независимы файлы P2-07/08/09; UI развития P2-13 доступен после 05. Это будущая параллельность, не запуск агентов сейчас. Shared patches принимает интегратор по очереди.
4. UI: P2-10 затем 11; P2-12 после 07…09. Макеты/ассеты готовятся независимо; UIManager, GameUi, entrypoints, локализация публикуются последовательно. P2-13 соединяется с CTA P2-10 до приёмки.
5. P2-14 связывает блоки; P2-15 закрывает диагностику; P2-16 принимает результат. Lead recompile/Play/CLI/test-level/capture — по одному и только при разрешённом запуске.

## Оставшиеся решения и зависимости

- **W1:** интегратор выбрал UTC первого durable-подтверждения; локальная квота и пределы защиты описаны в P2-04. Общая политика игрового дня/серверной защиты обсуждается в блоке 3; календарь Daily сохранён.
- **UX:** интегратор выбрал research 10…12: Level Up с двумя действиями, Daily в Quests с Later, HUD кольцо+секунды/комбинации. Композиция проверяется по эталонам и visual QA.
- **Технические зависимости:** принять отчёт блока 1 и освобождение shared-lock, проверить геометрию урока под 3 с, источник времени/межустройственную квоту, фактическую HUD safe area. Для получения аналитики нужна настройка UGS schema `first_session`. Серверная глобальная квота потребует отдельного контракта при таком продуктовом требовании.
- **Поздний этап:** замер темпа после Barcelona в 16; целевые дни/сессии до 13 уровня пока не заданы. Это не блокирует утверждённые 12 DP/2880 XP.

## Проверка документационного этапа

Прочитаны правила, обязательные источники,09, README, блок 1, goals/06/07. Узко сверены XP/Claim/weekly, каталог/runtime/дроп/HUD; получены API владельца блока 1. Источники UX прочитаны, указанные кадры просмотрены. Собственные изменения: только четыре Markdown. Относительные ссылки и существующие пути проверены; отсутствующие файлы явно запланированы как новые. Арифметика 1442/1450/1382/2880 и пороги идеальных открытий пересчитаны. Unity, compile, игровые тесты, сборки и публикация здесь не выполнялись.
