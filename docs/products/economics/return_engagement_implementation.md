# Блок 3 — реализованные активности возвращения

2026-09-09. `integration/unity-live`, локальный Unity Lead. Код, UI, конфигурация, compile и финальный review завершены. Игровые/визуальные прогоны по последнему поручению выполняет пользователь. [План](return_engagement_plan.md), [точный manifest](return_engagement_manifest.txt), исследования [14](research/14_home_activities_ui.md)/[15](research/15_return_rewards.md)/[16](research/16_activity_day_policy.md).

## Результат

- Семь накопительных Win-дней:10/10/15/15/20/20/50 монет; день7 также1 кристалл одной транзакцией. Цикл140+1; XP0. Повтор обычного уровня подходит; пропуски сохраняют шаг. День7 завершает цикл независимо отClaim, следующий шаг доступен первой победой более поздней даты.
- Цель недели:5 побед минимум в3 датах,50 монет. Отдельный прогресс, срок и следующая цель. Стартовый шаблон повторяется новыми недельными instances. Отдельной восьмой/рейтинговой выплаты нет.
- Новые периоды UTC00:00, неделя с понедельника. Часы устройства; local-journal модельL. Daily остаётся по локальной дате, W1 — UTC первого durable confirmation. UI показывает отдельное время обновления.
- Home: светлая плашка под логотипом, две строки, приоритет ready либо достижимой следующей цели. Play/SelectLevel и пять нижних маршрутов сохранены. Подробности — отдельный экран,7слотов и собственная недельная панель.
- Claim поimmutable snapshot сprofile/generation/ID/суммой. После записи квитанция; ACK отдельный. Окно открывается добровольно; pendingLevelUp/щит и уже открытый dialog сохраняют штатный приоритет.

Это выбранные интегратором стартовые настройки. Их достаточность, визуальная читаемость и влияние на прибыль ещё не измерены.

## Контракты реализации

Пути ниже относительно `LostCyberHamster/Assets/`.

- `Scripts/SharedCore/Meta/ReturnActivities/` —19 небольших domain/service/DTO файлов. `ActivityAttemptContext` получает GUID в production-попытке, сохраняет его technical journal до результата; revive использует тот же объект. `UiGameOverMechanics` передаёт контекст через синхронный completion scope. `LevelManager` сохраняет best/XP, `ActivityWinReceipt`, оба progress и earned в одной существующей транзакции; после commit публикует обновление. Дубли finish отсекаются. `LastCompletionExperience` сохраняет смысл квитанции XP блока2.
- `PlayerData.ReturnActivities`, `PlayerDataValidator` — безопасная миграция отсутствующего состояния без ретронаград. Текущая development migration блока2 не менялась. Validator проверяет схему, ID/суммы/периоды и уникальные квитанции.
- `ReturnActivityRecovery` сравнивает выбранный snapshot с owner journal, включая earned, claimed и свёрнутые Claim. Расхождение приостанавливает activity mutations. Явное действие в подробностях восстанавливает историю устройства, сохраняя баланс выбранного snapshot: replay валюты отсутствует. Текст объясняет этот выбор.
- Earned без срока. Claimed+presented циклы сворачиваются в закрытый диапазон; weekly — всписок закрытых IDs. Точные attempt IDs недели ограничены требуемым числом побед; последующие даты ещё учитываются. Текущая попытка/последний receipt защищают callback, новые дни сохраняются независимо от Claim.
- `Resources/ReturnActivities/return_activities.json` — каталогv1; вместо первоначально предложенного Content-path используется штатный `Resources.Load<TextAsset>` для маленькой синхронной конфигурации. Состав текущего цикла/недели закреплён; earned сумма не зависит от нового JSON. Некорректный каталог останавливает новые начисления, ранее earned остаются доступными.
- `Scripts/UI/ReturnActivities/` — formatter, selector, Home presenter. Новые `ReturnActivitiesScreenController`, `ActivityRewardModalController`; существующий MenuEntryPoint регистрирует маршруты и применяет очередь. Shared `UIManager`, `ResourceManager`, `GameDataManager` используют переданные API; дополнительных правок этих трёх файлов блок3 не делал.
- Новые2UXML/2USS переиспользуют имеющиеся shared panel/currency assets; новых bitmap нет. `Editor/ReturnActivityAssetSetup.Configure` черезUnity API регистрирует только2адреса UI и вызывает штатный regeneration. UI.asset получил2entries; bundle/content build не запускался. Все33новых asset/code файлов получили Unity metadata;4новые папки тоже.
- DEV и Tools: общий `ReturnActivityTestingRunner`, компактные секции в существующем XP/Level Progress Testing. Begin/Restore используют isolationAPI блока2; Win безXP, следующий/предыдущий UTC-день,+7дней, одинClaim иInspect. Мутации требуют активный изолированный профиль. Системные часы машины не меняются; внеDEV-профиля clock override не действует. Нового тестового каркаса нет.

## Аналитика

`ReturnActivityTelemetry` пишет business outbox внутри mutation, послеcommit передаёт SDK; повторный eventId пригоден для дедупликации. UI-exposure/open отдельны. Production/consent gate исключает Editor/Development/automation/test-profile. Изменение баланса уведомляет ResourceManager и существующие EarnCoins/EarnCrystals послеcommit.

Схема `return_activity`: строки `ra_event_id`, `ra_action`, `ra_kind`, `ra_period`, `ra_correlation`, `ra_policy`, `ra_trust`, `ra_app_version`; целые `ra_schema`, `ra_step`, `ra_wins`, `ra_days`, `ra_coins`, `ra_gems`, `ra_config`. Action:home_exposed/details_opened/eligible_win/day_credited/reward_available/claim_succeeded/cycle_completed/weekly_progress/weekly_completed/presentation_ack. Auth-ID и saveJSON не отправляются. При передаче SDK запись удаляется из domain outbox; это подтверждение приёма локальным SDK, а не серверный ACK UGS.

Dashboard-схема и приём событий UGS не настроены/не проверены этой задачей. До измерения нужно зарегистрировать схему и проверить реальные release-события. A30/D7/D30 берутся из session-дней research06, включаяLose; `day_credited` их не заменяет. Отдельного исследования/пересчёта всей финансовой модели не проводилось.

## Проверено

| Проверка | Результат |
|---|---|
| Финальный scoped review | Выполнен; архитектура, save/Claim/ACK, lifecycle, ID/period, UI-привязки и чужой baseline просмотрены |
| Regeneration | `46fd51ab896a4307a88756ae7ecb5000`, completed, точный requestId совпал |
| Runtime dotnet `--no-restore` | 0 ошибок,42 прежних предупреждения;4,63с |
| Editor dotnet `--no-restore` | 0 ошибок,17 прежних предупреждений;3,60с |
| Metadata/Addressables |33новых файла сmeta,4folder meta;2адреса зарегистрированыUnity API |
| Лёгкая проверка UI-данных | НовыеUXML разобраны XML;47RU/EN пар, прямые ключи иplaceholder пары проверены |
| Арифметика | Цикл140+1; W5/K0=70, W5/K1=120; W7/K2=240+1; W31/K5=845+4; зрелая фаза максимум900+5 |

Логи: `LostCyberHamster/EditorLogs/return_final_runtime.log`, `return_final_editor.log`, `return_unity.log`. Собственный UnityPID121940 остановлен после проверки пути/command line. APK, Play и ручное управление компьютером для реализации не использовались.

Review исправил: конфликт имёнUnity/AnalyticsEvent; относительныйSystem.Exception; размеры общей reward panel; повторныйACK при закрытии; cloud-сравнение заработанного и compacted claims. После последней C# правки повторён только regeneration+две затронутые сборки.

## Проверка пользователя и известные пределы

Игровые действия, Home/Details/модалка на реальном landscape, SafeArea/RU/EN, Next/Home/LevelUp/щит, Begin/Restore, offline/clock/cloud rollback и два устройства здесь не прогонялись. Чтение кода/компиляция не заменяют эти проверки. Баланс, удержание и прибыль не измерены; пилот/A-B подготовлены планом, фактического rollout не было.

L сохраняет локальную однократность; смена часов вперёд и два независимых устройства глобально не защищены. Возврат часов нижеhigh-water приостанавливает новые зачёты, оставляяClaim earned. Строгие серверныеS1/S2 не активированы рабочим решением; доверенное время/atomicwallet требуют отдельного backend scope.

## Git-передача

Собственныйmanifest:86Assets/code/meta +7документов =93пути. Включает README после последовательной передачи от2. Совместный commit включает готовые manifests1/2/3 и whitelist документов основного чата. `docs/game_economy.md`, `docs/rules/AGENTS.md`, generated `.csproj` с подтверждённым локальным шумом остаются внеstaging. Финальный SHA/remote сообщаются после push; отчёты1/2 сохраняют фактические результаты своих этапов.

Интеграционный commit `a6a6e44a`:259 согласованных файлов. Первый push обнаружил четыре новых HUD-коммита в remote до `3c83d8b6`; merge сохранил обе стороны без конфликтов. Совместные HUD C#/UXML/USS просмотрены. После merge Unity import+regeneration завершились с кодом0; Runtime0ошибок/42warnings за5,11с, Editor0/17 за3,64с. Логи: `return_merge_unity.log`, `return_merge_runtime.log`, `return_merge_editor.log` в `LostCyberHamster/EditorLogs`. Игровой прогон остаётся пользователю. Проверка Git whitespace чистая для авторских файлов; пробелы пустых полей в Unity-generated `.meta` сохранены.
