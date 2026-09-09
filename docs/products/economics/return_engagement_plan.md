# Блок 3 — план возвращений

2026-09-09. **Исследования завершены; пользователь поручил полную реализацию и затем commit/push.** P3-00…12 выполняются последовательно в этой задаче, без создания новых пользовательских задач. S1/S2 условные, режим S не выбран. Владелец документа и финальный Git-интегратор: `01a08740-aadb-7e83-bdbe-6a0c46f1dcf5`, host local. Общие решения: `01a085eb-cce0-75e0-ac47-e1e1d283cc92`.

P3-00…10: реализованы;11 — парные ручные DEV/Tools-команды и compile/review готовы;12 — бюджет и передача готовы, фактические пилот/UGS/игровые проверки остаются пользователю по его последнему поручению. Все shared переданы блоком2 до правок. Финальные Runtime/Editor:0ошибок,42/17прежнихwarnings. Точная реализация, изменения относительно кандидатов и пределы проверки — [отчёт](return_engagement_implementation.md), файлы — [manifest](return_engagement_manifest.txt). Длинные и ручные прогоны, новые тестовые каркасы и сборки для проверки пользователь исключил; ниже они сохранены как сценарии его будущей приёмки.

Основа и мотивация: [передача13](research/13_return_engagement.md). Результаты: [Home14](research/14_home_activities_ui.md), [награды15](research/15_return_rewards.md), [день/Claim16](research/16_activity_day_policy.md). Факты экономики — [README](README.md), целевая прогрессия — [09](research/09_progression_abilities.md), очередь/представление — [10](research/10_level_up_ui.md), [11](research/11_daily_reward_ui.md), [12](research/12_ability_hud_ui.md).

## Результат и границы решения

Цепочка цели: максимальная устойчивая чистая прибыль, минимум $2000/месяц к31.12.2026; доход на установку при прибыльном привлечении; A30≥5, D7≥15%, D30≥7%; понятные причины возвращаться. Метрики — рабочие гипотезы [06](research/06_retention_targets.md)/[07](research/07_monetization_targets.md). Новый бесплатный ресурс оценивается вместе с замещением рекламы/покупок, а не одним ростом Claim.

Согласованная основа13: семь накопительных победных дней без потери за пропуск, монеты/кристаллы за каждый день; самостоятельная календарная недельная активность; компактное представление двух активностей на существующем Home. Новые XP/DP, push, рейтинговые призы и welcome-back вне порученной основы.

**Рабочий выбор интегратора для реализации по поручению пользователя:**

- Цикл: монеты10/10/15/15/20/20/50, на дне7 ещё1 кристалл одной выдачей; всего140+1. День7 — усиленный итог, отдельной восьмой выплаты нет. Прогресс и rollover независимы от Claim; earned без срока.
- Неделя:5 Win в≥3 разных днях;50 монет. Стартовый шаблон один, повторяется новым instance каждую неделю; следующая цель/дата видна заранее. Вариант6 звёзд/3дня исследован15 как последующее расширение контента.
- Home: плашка под логотипом, две строки с явными названиями/прогрессом; доступная награда первая; подробности отдельным экраном. Ручной Claim и добровольное подтверждение; Play/SelectLevel сохраняют главный маршрут.
- Новые активности: UTC00:00/понедельникUTC; для ограниченного пилота локальные часы и журнал L. Существующие Daily local и W1 UTC остаются явными разными источниками. Сравнение с единым local-днём и цена строгого S —16. UTC сам не повышает доверие к часам.

Выбраны суммы/5÷3/один шаблон, UTC и локальный L, композицияA. Это рабочие решения интегратора, а не отдельное численное утверждение пользователя. Строгий S потребует отдельного расширения backend/кошелька. ЭталонA готовится до UI-интеграции. По правилам code_conventions новые unit/EditMode/PlayMode тесты не создаются; указанные domain/fault сценарии служат scoped-review и парным DEV/Tools-командам через production-логику. Финальный C# gate — regeneration и dotnet; фактические внешние проверки отмечаются только после выполнения.

## Проверенная передача и владение

Рабочая ветка проверена: `integration/unity-live`, локальный Unity Lead. Владелец2 `01a08704-633b-7681-8cc6-9c87075be90c` держит integration lock и реализует [progression_plan](progression_plan.md). Его код/ассеты/план/README/lock в этой работе не менялись. Собственный scope: только этот документ и14–16. Общий dirty state содержит незавершённые файлы1/2 и чужие документы; это не diff блока3.

Передача2 от09.09 и узкое чтение:

| Контракт | Использование блоком3 |
|---|---|
| `LevelManager.CompleteLevel(levelKey,stars)`, `LastCompletionExperience` после commit | Best/XP сохраняются транзакцией. XP receipt не уникален для Win; собственный qualifying receipt/hook внутри той же транзакции, repeat с XP0 тоже подходит |
| `GameDataManager.ExecuteTransaction`, owner journal, profile/generation | Общий save boundary. Вложенный transaction недопустим; UI и события после commit |
| `DailyCommonRewardSnapshot`, `QuestManager.GetDailyCommonReward/ClaimDailyCommonReward(snapshot)` | Принцип предъявленного immutable snapshot; новая активность не расширяет QuestManager новыми квестами |
| `WeeklyLeaderboardRun.RewardDecision`, owner/environment/date0/5 | Локальный quota, не trusted-clock/global-server guarantee. Новая недельная цель имеет свой namespace |
| `UIManager`, `PlayerLevelPresentation`, `FirstSessionNavigation` | Один modal host; ack+route отдельно от выдачи; сохранённый урок/продолжение, scene/profile/generation guards |
| `PlayerData.SuperAttackLevels`, migration v3, `TryUpgradeSuperAttack`, runtime Snapshot | Активные изменения блока2; новая schema мигрирует поверх его финальной версии. Активностям не требуется способность |
| `AddCoinsOrBonusMechanics` | Bonus начисляется сразу при выпадении; физический collectible отдельно. Полный бюджет15 не дублирует bonus |

Блок1 по [отчёту](first_session_implementation.md) завершил код/compile. UI/Play/phone, фактические90с и приём UGS `first_session` остаются непроверенными. Финальную передачу2 и актуальные signatures получить P3-00; текущий код2 нельзя считать финально принятым.

Владелец2 подтвердил добавление ссылки на13 в README своим обновлением. Ссылки на готовые14–16/план передаются ему/основному чату; параллельная правка индекса не выполняется.

## Общий контракт будущих задач

Пути ниже относительно `LostCyberHamster/Assets/`; `docs/` — от корня репозитория. «Новый» — проектируемый файл, сейчас отсутствует. Имена API кандидаты; P3-00 фиксирует окончательные. Владельцем новых файлов становится исполнитель соответствующего ID; shared правит интегратор после передачи от2. Переход владения между P3-задачами явный.

- До каждой правки и commit: общий/scoped `git status`, актуальная передача, integration lock по правилам проекта. Работа в Lead; UI-задачи high и закреплены при фактическом создании пользователем. Сейчас новые задачи не создаются.
- Базовая приёмка каждого ID: scoped review, точные reward/attempt IDs, save до событий, profile/generation guards. Проверки только своих изменений. C# gate — актуальные правила `docs/rules/unity_cli.md`/`code_conventions.md`; Unity/Play/phone после отдельного разрешённого запуска и свободного lock.
- State machine общая: attempt; committed qualifying Win; credited day/progress; available entitlement; claimed wallet receipt; presentation ACK. DTO UI не создаёт ни Win, ни award. XP0 для активности, несмотря на возможный XP той же победы от блока2.
- Требуемые проверки listed ниже — будущая работа. В рамках текущего планирования выполнены только статическое чтение, исследования, арифметика/ссылки документов; игровой код не исполнялся.

Порядок: P3-00 →01 →02 →03/04 →05 →06; P3-07 после01 и передачи Home; P3-08 после05/07; P3-09 после06/08 и финала2; P3-10 после02/05; P3-11 после09/10; P3-12 после11. Независимость домена/макета допускает файловую подготовку в своём scope; shared/Unity идут последовательно. Если выбран S, P3-S1/S2 становятся обязательной зависимостью05/06 и закрываются до релиза.

## P3-00 — Зафиксировать решения и принять shared

**Игрок:** понятные единые правила активности и устойчивое продолжение текущей игры.

**Файлы/владение:** собственные `docs/products/economics/return_engagement_plan.md`, research14–16; чтение финального отчёта2, `PlayerData.cs`, `PlayerDataValidator.cs`, `LevelManager.cs`, `UIManager.cs`, `MenuEntryPoint.cs`, HomeUXML/USS. Shared пока у2, запись начинается только после передачи.

**Результат:** записать выбранные суммы, условие, timezone/trust режим, supported landscape, окончательные hooks и state schema; заменить кандидаты фактическими контрактами. Принять baseline по каждому shared файлу, выяснить final migration version и policy W1/Daily. Закрепить список новых источников/Addressables/локализации и маршрутов.

**Сохранение/приёмка:** прошлые награды/XP/урок/route остаются воспроизводимыми из baseline. У каждой общей правки есть владелец; решения пользователя отделены от рекомендаций. Проверка: scoped status, финальный отчёт2, сверка signatures/версий и чек-лист зависимостей; игрового запуска для этой задачи не требуется.

## P3-01 — Домен, конфигурация и игровой день

**Игрок:** знает, какой день/неделя действует и когда доступен следующий шаг.

**Порядок:**00. **Новые файлы, владелец исполнитель01:** `Scripts/SharedCore/Meta/ReturnActivities/ActivityDayPolicy.cs`, `ActivityPeriod.cs`, `ReturnActivityState.cs`, `ActivityReward.cs`, `ReturnActivityConfig.cs`, `ReturnActivityConfigLoader.cs`; `Content/return_activities/return_activities.json`. Shared регистрация контента — интегратор позже08.

**Результат:** timezone/trust provider, calendar boundary, IDs, immutable config v1 и pinned cycle/week snapshots. Утверждённые числа15 читаются из конфигурации и UI. Дата/период не зависят от локали форматирования. Следующий недельный instance получает template и срок детерминированно.

**Сохранение:** schemaVersion, policyVersion, featureEpoch, lastCreditedDay/high-water; валидировать суммы≥0,7 шагов, XP0, weekly5/3/50, уникальные IDs. Existing earned сохраняет исходный состав после config-update.

**Приёмка/проверки:** boundaryUTC либо выбранного local, смена месяца/года/DST/timezone; день7 допускает новый цикл только поздней датой; high-water политика16. Проверка invalid/missing config с безопасным выключением новых мутаций и сохранением earned. C# gate; чистые policy-тесты на фиксированных датах, без Unity Play.

## P3-02 — Durable Win receipt и восстановление

**Игрок:** первая настоящая победа засчитывается после сохранения; повторный callback награды не удваивает.

**Порядок:**01, final2. **Новые файлы:** `Scripts/SharedCore/Meta/ReturnActivities/ActivityWinReceipt.cs`, `ActivityAttemptContext.cs`, `ReturnActivityJournal.cs`, `ReturnActivityService.cs`. **Shared через интегратора:** `Scripts/GameManagement/PlayerProgress/PlayerData.cs`, `Scripts/GameManagement/Persistence/PlayerDataValidator.cs`, `CheckpointReason.cs`, `Scripts/System/LevelManagement/LevelManager.cs`, `Scripts/GameEngine/Mechanics/UiGameOverMechanics.cs`, `Scripts/CloudSave/ConflictService.cs`/`GameDataManager.cs` только необходимый recovery hook.

**Результат:** стабильный owner+attempt ID с начала production-попытки, сохраняемый revive и заменяемый Restart/Next; mode/scene generation guard. Hook внутри `LevelCompleted` transaction сохраняет qualifying Win, mutation двух активностей и event outbox; последующая публикация snapshot. Действующая XP receipt сохраняет семантику блока2. Допустим повтор обычного уровня с XP0; stars0/Lose/tutorial/automation явно фильтруются по реальному режиму.

**Сохранение:** миграция старого профиля начинает цикл0/новую неделю0 без ретронаград по best-stars. PlayerData и owner journal — контракт16; claimed/earned IDs переживают выбор snapshot по согласованной recovery-policy. Расхождение old-cloud с локальным claim журналом блокирует activity mutation до разрешения, а не автоматически replay валюту.

**Приёмка/проверки:** одна попытка два finish, две разные попытки одного уровня, revive+Win, crash до/после save, исключение внутри transaction, cold resume, смена owner, automation, старый cloud. Проверить отсутствие вложенного save и публикаций до commit. ID очистка сохраняет дедупликацию после compaction; pending100+ наград не теряются. C# gate и focused persistence/fault runner11.

## P3-03 — Семь накопительных победных дней

**Игрок:** получает следующий шаг за первую победу дня; возвращается после пропуска к тому же прогрессу.

**Порядок:**02. **Файлы:** новые `Scripts/SharedCore/Meta/ReturnActivities/SevenWinDaysPolicy.cs`, `SevenWinDaysSnapshot.cs`; переданные01/02 `ReturnActivityService.cs`/state только в этой последовательной фазе.

**Результат:**0–7 earned steps, nextAction snapshot, один день на дату через границу циклов. На7-м создаётся один entitlement50+1; next cycle ждёт следующую дату, Claim не блокирует. UI различает earned-step и claimed-step.

**Сохранение:** cycleId, pinned rewards, lastCreditedDay, earned IDs; пропуск7/30 дней сохраняет step. Поздний Claim старого цикла не меняет текущий.

**Приёмка/проверки:**0/1/6/7, пропуски, пять Win одной даты, day7+ещёWin, следующий день безClaim7, три цикла с backlog, config-update. Сумма одного цикла140монет/1кристалл/0XP. Domain scenarios+C# gate; crash receipt контролируется02/11.

## P3-04 — Самостоятельная цель недели

**Игрок:** видит5 побед/3 дня, награду50, срок и следующую цель; прогресс отличается от цикла.

**Порядок:**02; совместная запись service после03. **Файлы:** новые `Scripts/SharedCore/Meta/ReturnActivities/WeeklyActivityPolicy.cs`, `WeeklyActivitySnapshot.cs`; переданные config/service/state01–03.

**Результат:** один стартовый template5/3, собственные instance/week IDs; один Win может продвинуть оба источника. Новый период обновляет только weekly instance. Недостижимая по оставшимся датам цель честно отдаёт remainingDays/nextWeek в snapshot. Будущий шаблон6звёзд/3дня остаётся расширением после решения, а не скрытым scope.

**Сохранение:** wins dedupe поattempt, множество дат, completion entitlement со snapshot50. Старые earned остаются; незавершённая неделя не создаёт награду. При позднем событии использовать сохранённый receipt period, правила L/S из16.

**Приёмка/проверки:**5Win/1день не completed;4Win/3дня не completed;5Win/3дня completed один раз. Повтор одного уровня подходит. Неделя с Sunday/Monday,7 дней поперёк двух недель, первый входSunday, две old-ready+новая цель, смена года. Review/domain+C# gate.

## P3-05 — Получение валюты по snapshot

**Игрок:** забирает предъявленную награду один раз; может отложить без потери прогресса.

**Порядок:**03/04; S1/S2 если выбран S. **Новые файлы:** `Scripts/SharedCore/Meta/ReturnActivities/ActivityRewardSnapshot.cs`, `ActivityClaimResult.cs`, `ReturnActivityRewardService.cs`. **Shared:** `Scripts/SharedCore/Meta/Storages/ResourceManager.cs` только если нужен общий post-commit callback; PlayerData/journal из02 через текущего владельца.

**Результат:** `GetRewards`/`Claim(expectedSnapshot)` по16; payloadUI не задаёт сумму. `available/claiming/claimed/saveFailed/staleContext/syncRequired` различаются. День7 две валюты одной транзакцией; дневная и weekly награды разными IDs. Старые earned выбираются явно.

**Сохранение:** кошелёк+claimedReceipt+journal+analytics outbox атомарно; события после commit. Повтор возвращает квитанцию без новой выдачи. Поздний ACK не влияет на кошелёк. Guest/account и stale generation обработаны.

**Приёмка/проверки:** двойное касание, save exception, закрытие после commit до ответа, другая награда в очереди при открытом окне, midnight/config-update, смена profile, обе available. Сравнить баланс/ID до и после, XP неизменен. Fault runner11 и C# gate. При S два реальных клиента проверяют общий balance revision.

## P3-06 — Cloud/offline и совместимость с блоком2

**Игрок:** понимает, что сохранено и что ждёт связи; ранее полученное не выдаётся повторно.

**Порядок:**05, final2; S1/S2 по решению. **Новые файлы:** `Scripts/SharedCore/Meta/ReturnActivities/ReturnActivityRecovery.cs`, `ReturnActivitySyncState.cs`. **Shared через интегратора:** `Scripts/CloudSave/ConflictService.cs`, `Scripts/GameManagement/Persistence/GameDataManager.cs`, `PlayerDataValidator.cs`, `Scripts/Entry Points/MenuEntryPoint.cs`. Daily/WeeklyLeaderboard файлы в режимеL только читаются.

**Результат:** owner mapping при linking; cold offline, restore/merge, outbox и UI availability по16. L поддерживает local Claim; S требует серверного подтверждения. Два режима не переключаются от наличия сети. Existing Daily local/W1 first-confirmation остаются прежними.

**Сохранение:** старый cloud не повторяет claimed; conflict resolution фиксирует baseline и receipt reconciliation до нового Claim. Компактация закрытых IDs, pending без expiry. Новый GameDay adapter может быть интерфейсом, но миграцию соседей выполняет только отдельный согласованный scope.

**Приёмка/проверки:** cold offline, online→offline, reconnect, rollback, two-device conflict, guest link/new account, future-clock correction. Под L явно зафиксировать воспроизводимый предел multi-device, под S доказатьglobal dedupe. Проверки offline разделяют реальное устройство/отключение сети и SDK simulation; сейчас ни одна не выполнена.

## P3-07 — Утвердить композицию и подготовить UI-ассеты

**Игрок:** видит две цели без потери заметности Play.

**Порядок:**01, передача Home от2; выполнение вне Unity возможно до05. **Файлы/владение:** research14 и внешняя рабочая папка экранного эталона, которую фиксирует00; новые финальные `Content/ui/sprites/return_activities/` только при необходимости. Существующие `sprites/shared/*` используются ссылками; изменение shared согласуется.

**Результат:** эталонA на текущем Home, вариант узкого landscape; подробности, Claim/receipt, seven-day final, offline/loading; dynamic numbers отдельно. Переиспользовать cream panel/контуры/currency-assets. Подготовить PNG alpha/9-slice до Unity-интеграции по UI-гайду.

**Сохранение:** данные/награды графикой не закрепляются; rewarded icon состояния берутся из snapshot. **Приёмка/проверки:** все состояния14,16:9/19.5:9/20:9/4:3, RU/EN/Back/touch; alpha на светлом/тёмном/checkerboard и9-slice в трёх размерах. Независимый visual QA после правок — по UI-гайду. Одобренный эталон и список assets переданы08, не только картинка без states.

## P3-08 — Home и постоянные подробности

**Игрок:** видит ближайшее выполнимое действие, открывает обе активности и старые ready-награды.

**Порядок:**05/07, final2. **Новые файлы:** `Scripts/UI/Screens/ReturnActivitiesScreenController.cs`, `Scripts/UI/ReturnActivities/HomeActivityPresenter.cs`, `HomeActivitySelector.cs`, `Content/ui/uxml/ReturnActivitiesScreen.uxml`, `Content/ui/styles/screens/ReturnActivitiesScreen.uss`. **Shared/существующие через интегратора:** `HomeScreenController.cs`, `HomeScreen.uxml`, `HomeScreen.uss`, `Scripts/UI/Common/ScreenEnum.cs`, `Scripts/Entry Points/MenuEntryPoint.cs`, `AddressableAssetsData/AssetGroups/UI.asset`, `Content/localization/lang.ru.json`, `lang.en.json`.

**Результат:** композицияA, selector14, две строки без автокарусели; первый tap открывает детали, Claim отдельным действием. Binding от единого snapshot после commit/resume/date; стабильный reward target. Back/Home/Play используют текущие маршруты.

**Сохранение:** только временный selection/scroll вUI, entitlement всегда domain. Generation guards и Dispose/subscribe без накопления callbacks. **Приёмка/проверки:** обе available,1победа+1будущийдень,6/7 и7/7, новой недели,UTC-vs-Daily подписи, narrowRU, offline/unknownprofile. XML/локализация/Addressables refs, C# gate; visual/runtime по разрешённому запуску11. Существующие кнопки Home проходят каждый маршрут.

## P3-09 — Claim-модалка, квитанция и очередь представлений

**Игрок:** получает понятное подтверждение, сохраняет выбранный Next/Home/урок; повторный popup не мешает каждой победе.

**Порядок:**06/08, передача queue2. **Новые файлы:** `Scripts/UI/Modals/ActivityRewardModalController.cs`, `Scripts/UI/ReturnActivities/ActivityRewardPresentation.cs`, `Content/ui/uxml/ActivityRewardModal.uxml`, `Content/ui/styles/ActivityRewardModal.uss`. **Shared:** `Scripts/UI/Common/UIManager.cs`, `ScreenEnum.cs`, `Scripts/Entry Points/MenuEntryPoint.cs`, UI Addressables иRU/EN из08. `PlayerLevelPresentation`/`FirstSessionNavigation` меняются только при подтверждённой необходимости с их владельцем.

**Результат:** добровольный modalClaim/Later, отдельный receiptACK, day7 одна квитанция50+1; уже открытый dialog завершается перед следующим. PendingLevelUp/щит сохраняют приоритет и ack+route. Win не запускает обязательный новый activity-dialog; Home ready и детали дают постоянный вход.

**Сохранение:** ClaimID и presentedReceipt/ACK разделены. При failure загрузки доступен исходный экран; при success save/failure animation восстанавливается квитанция. Переход вызывает один guarded continuation.

**Приёмка/проверки:** открытыйDaily+lateW1, LevelUp/щит послеWin, Next/Restart/Home, Claim7+weekly, Later/back/coldstart, новыйprofile при asyncload. Одна модалка/один переход, баланс не зависит от повторного показа. Проверить clip-owner, RU/EN, анимацию/skip и реальные touch. C# gate + матрица11.

## P3-10 — События и измерение результата

**Игрок:** получает проверяемые улучшения баланса/понятности по фактическому поведению.

**Порядок:**02/05; подключение UI после08/09. **Новые файлы:** `Scripts/SharedCore/Meta/ReturnActivities/ReturnActivityTelemetry.cs`. **Shared:** `Scripts/Analytics/Events.cs`, `AnalyticsManager.cs`, нужные mutation/UI hooks; схема UGS Dashboard отдельным авторизованным действием. Результат схемы хранить в этом плане/будущем отчёте.

**События:** `return_activity` с `action=home_exposed/details_opened/eligible_win/day_credited/reward_available/claim_succeeded/cycle_completed/weekly_progress/weekly_completed/presentation_ack/sync_error`; обязательные version, policy/trust, kind, periodId, cycleId/step, counts, coins/gems, source, durable eventId, attempt/reward correlation где применимо. Generation/owner guards на клиенте; analytics identity по существующему consent-контракту. Полные auth-ID/сырые saveJSON в payload не передавать.

**Сохранение:** business-события в outbox той же транзакции, доставки могут повторяться — dedupe поeventId. UI-exposure один раз на видимое посещение/новыйsnapshot; тики countdown не являются экспозициями. `reward_available`, `claim_succeeded` и `presentation_ack` не считаются победами.

**Метрики:** воронка exposure→Win-day→earned→Claim; weekly5/3 отдельно; A30/D7/D30 из всех реальных игровых сессий поUTC research06, включаяLose. Не вычислять A30 из return-day. Доход/first_open, rewarded поplacement, IAP когда внедрён, source/sink монет/кристаллов, время до purchase, часы/два устройства по trust-mode.

**Приёмка/проверки:** schema/types вUGS, приём контрольных событий в Dashboard и export/dedupe, consent/offline/retry, version tags и исключениеDEV. Одного метода Send недостаточно. Контрольный профиль:2Win одной даты, одинClaim, restart; ожидается1day/1grant при2eligibleWin. Отдельный session-with-Lose увеличивает session-day, не win-day. Исходные реальные метрики пока отсутствуют.

## P3-11 — Парные DEV/Tools и интеграционная приёмка

**Игрок:** прогресс, выдачи и переходы выдерживают реальные границы времени/сохранения.

**Порядок:**09/10. **Новые файлы:** `Scripts/DevTools/ReturnActivityTesting/ReturnActivityTestRunner.cs`, `ReturnActivityTestingScreen.cs`, `ReturnActivityTestingView.cs`; `Editor/Testing/ReturnActivityTesting/ReturnActivityTestingPage.cs`. Регистрация обеих поверхностей — через существующий registry, точный файл определить00. Документ результатов: новый `docs/products/economics/return_engagement_implementation.md`.

**Результат:** один production runner для fixture clock, attempt, failure-point, restore; одинаковые действия и состояния DEV/Tools. Смена часов применяется через тестовый provider вDEV scope, не системное время машины. Test profile иnamespace изолированы от production.

**Сохранение/матрица:**1)7Win-дней/пропуски/rollover безClaim;2)weekly5/1,4/3,5/3,две недели;3)двойнойWin/Claim,crash до/после save/ACK;4)oldcloud/owner/link/offline/reconnect;5)clock/week/year;6)LevelUp/Daily/щит/Next/Home;7)UI размеры/RU/EN;8)бюджет иUGS. Обе DEV/Tools поверхности после входа/выхода Play показывают одинаковый production state.

**Проверки:** scoped review, C# gate, разрешённый Play/визуальный/телефонный проход последовательно с lock, Console во время реального пути. Для S — concurrency tests двух клиентов/реального backend и wallet. Для L report явно оставляет известные global-риски. ВремяL1/90с остаётся приёмкой блока1; этот блок не объявляет его проверенным автоматически.

## P3-12 — Пилот, бюджетный гейт и передача

**Игрок:** активность выходит с проверенным бюджетом и работающими наградами.

**Порядок:**11; S-гейты обязательны при выбореS. **Файлы/владение:** свой implementation-report/план/research15; README экономики обновляет согласованный единственный владелец. Настройки rollout/UGS — после явного поручения публикации. APK/доставка/commit/push не следуют автоматически из этого плана.

**Результат:** сохранить build/config/policyVersion и результаты матрицы, отчёт own/shared diff, оставшиеся зависимости1/2. Малый технический пилот проверяет выдачи/стабильность, затем сопоставимые контроль/вариант когорты проверяют эффект. Размер и длительность A/B рассчитываются поbaseline/MDE, не произвольным5игрокам UX. Research06 ориентирует первый D30-срез≥3000новых с полным наблюдением; этого числа недостаточно для гарантии мощности каждого сегмента.

**Сохранение:** kill switch останавливает новые earning instances по утверждённой версии; earned/Claim recovery сохраняются. Каталожное уменьшение новых rewards не меняет уже earned-суммы. МиграцияL→S отдельная, не rollback feature flag.

**Приёмка:** ноль необъяснённых повторных выплат/потерь вматрице; подтверждённый приёмUGS; совпадение бюджета140+1/50 и источников; Home/Play/Claim понятны. Продуктовый rollout оценивает A30, D7/D30 и netR30 вместе с валютным потоком и расходами backend; ростClaim сам по себе недостаточен. При недостаточной зрелости когорты отчёт пишет «эффект не измерен», не «цель достигнута».

## P3-S1 — Условная задача: серверные периоды и entitlement

**Условие:** пользователь выбирает строгую глобальную квотуS. **Игрок:** одна календарная награда владельцу при двух устройствах.

**Порядок:**00/01, до05. **Новые server-owned файлы кандидаты:** `services/return-activities/` от корня репозитория: `ActivityCommands.cs`, `ActivityStateStore.cs`, `ActivityContractTests.cs`, deployment/config manifest; клиент `Scripts/SharedCore/Meta/ReturnActivities/ReturnActivityServerGateway.cs`. Точный host/runtime/package выбирается после spike16; секреты вrepo не хранятся.

**Результат/сохранение:** GetState/SubmitWin/Claim по16, serverUtc, authenticated owner, command dedupe, atomically persisted entitlement и quota, protected writes, version/CAS. Первый concurrent owner-create защищён доказанным механизмом. Backend approval/deploy — отдельная финальная авторизация после review, если ещё не дана.

**Приёмка/проверки:** concurrent5/3 submit, дваClaim одногоID, timeout послеcommit, unauthorizedowner, неправильныйperiod, новаяweek, initialcreate race. Серверное принятие не выдаётся за доказательство честности client gameplay. Receipt+quota защищены; wallet/S2 ещё обязателен для end-to-end. Rough дополнительный объёмS1+S2:8–15 инженерных дней, уточнить spike.

## P3-S2 — Условная задача: authoritative wallet и offline outbox

**Условие/порядок:**S, послеS1 и до05/06. **Игрок:** подтверждённые деньги и покупки согласованы между устройствами, offline-результат имеет понятный статус.

**Файлы:** server-owned `services/return-activities/ActivityWallet.cs`, `WalletOperationStore.cs`, client gateway/recovery изS1/06; shared `ResourceManager.cs`, `PlayerData.cs`, `GameDataManager.cs`, `CloudSave/ConflictService.cs`, владельцы Shop/Skins/Rewarded при переходе их валютных операций. Это расширение ownership экономики, согласуется основным чатом отдельно.

**Результат/сохранение:** единый atomic reward+wallet/spend контракт либо доказанный журнал всех currency operations; запрет старому full-snapshot overwrite authoritative balance. Outbox Win/Claim с устойчивымиcommand IDs, серверная дата первого принятия, pendingoffline UI. Migration guest/L snapshots, grandfathered rewards иlegacy receipts по согласованнойcutover.

**Приёмка/проверки:** два устройства получают/тратят/возвращаютoldcloud; сумма не удваивается, повторspend не применяется. Offlineчерез3дня не обещает3наградныхдня при одной датеserveracceptance; midnightretry уже принятого события сохраняет дату. Контроль partial failure междуrewardиwallet, backenddown, запретclientwrite. Без этого этапа можно заявить только серверную квоту entitlement, не строгую защиту общей валюты.

## Что проверено в текущей задаче

Прочитаны правила/обязательные источники, экономика/13/10–12/отчёт1/план2 и узкие владельцы Home/Win/save/reward. Контракты запрошены и получены напрямую от2. Просмотрены4локальныхспрайта и2внешнихкадра;3аналога подтверждены прямыми источниками. Подготовлены четыре документа; арифметика/ссылки/scoped изменения проверены отдельно. Код, Unity, sharedlock, чужие планы иREADME не менялись. Визуальный эталон, gameplay/phone, эффект retention/прибыль остаются будущей приёмкой.
