# Статистика плейтестов — реализация

Дата: 2026-09-10. План: [T1–T4](telemetry_plan.md). Инструкция агента: [tools/economy](../../../tools/economy/README.md).

## Реализовано

- T1: локальная телеметрия development Android; snapshot/transaction/run/session/first-session/monetization/return-view. Штатные награды, списания и сохранения сохранены. Учебный временный кошелёк изолирован; итоговые XP обучения фиксируются после восстановления настоящего профиля.
- T2: JSONL-пакеты, ограниченная очередь между запусками, повтор через `OnlineServicesCoordinator`, проверка ACK/hash. Collector пишет архив отдельно от diagnostic retention. Docker receiver обновлён; local/public health и контейнеры healthy.
- T3: Python-скрипт без зависимостей, фильтры профиля/периода/баланса/cohort, дедупликация, конфликты/пропуски, `summary.json` с происхождением чисел. UI и новая UGS-схема относятся к следующим этапам.
- DEV/Tools/Testing: общие runner-ы подготовки XP/прохождений, квестов и скинов помечают искусственные операции. Список помеченных профилей хранится в sidecar и переживает переключение профилей/перезапуск.
- T4: целевые проверки логики/доставки на компьютере выполнены; Android APK и пользовательская приёмка отслеживаются ниже.

## Точки записи

| Источник | Подтверждение / файлы |
|---|---|
| Первая победа, новые звёзды, очки за повышение | `GameDataManager.ExecuteTransaction`, `LevelManager.CompleteLevel`; изменение общего XP из фактических уровня и остатка |
| Daily, Story, общий Daily, weekly | Причины `QuestRewardClaimed`, `DailyQuestCommonRewardClaimed`, `WeeklyLeaderboardRecordRewarded`; ID в `detail` и снимках |
| Возвращаемые активности | Состояние дней/наград и квитанции после transaction; `ReturnActivityTelemetry.RecordView` для показа |
| Rewarded, покупки, обмен, развитие, refill, revive | Те же post-commit hooks; игровые квитанции, расход и остатки; `MonetizationEvent.Record` для воронки |
| Обучение | `TutorialSession.RestoreSnapshot` после очистки временного backup; `FirstSessionTelemetry.Record` для этапов |
| Добыча | `ResourceManager` после успешного начисления, source из `GameEventsManager`; причины в pickup/jump/bonus producers |
| Попытки и время | `OnLevelStarted`, post-commit Win, окончательный Lose, unload сцены; `GameManager.State == PLAYING`, focus/background |
| Прерывания и расхождения | Sidecar с последним checkpoint/попыткой, сравнение после загрузки; разрыв записывается явно |

Файлы реализации: новые `Diagnostics/EconomySnapshot.cs`, `EconomyTelemetry.cs`, `EconomyJournal.cs`; узкие hooks в таблице, настройки/загрузчик, collector/compose/ensure, `tools/economy/`. Generated csproj и `.meta` созданы Unity. В начале задачи dirty были только собственный план и ссылка из economics README.

## Проверки и доказательства

- Python: 5 проверок — XP rollover, повтор загрузки, доход/расход, время, conflict/gap/incomplete, повтор операции, разделение DEV. Детерминированный повтор отчёта проверен.
- Node HTTP: ACK после записи, повтор одного пакета, отклонение неправильного hash/оборванной строки, сохранность архива после удаления старой диагностики.
- Unity: regeneration после импорта новых файлов, `dotnet build Assembly-CSharp.csproj --no-restore`, 0 ошибок. Первый gate до импорта не видел новые Compile Include; после `AssetDatabase.Refresh` и regeneration файлы присутствуют.
- Живой Editor, изолированный штатный testing profile: +100 монет; списание с исключением откатилось; подтверждённая трата 80; первая победа +31 XP; повтор +0; уровень 2/остаток 21 после исходных 230 XP. Исходный профиль и сохранённый JSON восстановлены точно.
- Эти события прошли реальный HTTPS endpoint и Docker collector в Dropbox. `summary.json` подтвердил +31 XP, +100/−80 монет и отсутствие разрывов тестового профиля.
- При остановленном collector остался пакет 1251 байт. После остановки/запуска Play и восстановления collector запись `offline_restart_probe` появилась в архиве; ожидающих пакетов 0.
- Bootstrap достиг Menu; telemetry включалась для проверки только временно. Console: 0 ошибок. После проверки `allowInEditor: false` восстановлено.
- После финальных C#-правок повторены regeneration/compile (0 ошибок, 44 предупреждения проекта) и Bootstrap → Menu → stop (Console: 0 ошибок). Отдельная проверка DEV-маркера подтвердила его сохранность после смены профиля и точное восстановление исходного сохранения (`.temp/telemetry-dev-result.json`).

Локальные доказательства: `.temp/telemetry-runtime-result.json`, `.temp/telemetry-runtime-summary.json`, `.temp/telemetry-compile.log`, `.temp/telemetry-collector-test.log`, `.temp/telemetry-stack.log`. Архив тестового профиля: `6edfe746730c4cc4967f3fbc85c54e63`, cohort `editor`; обычные Android-отчёты его исключают.

## Android-приёмка

Сборка завершена через `tools/build/build_android_telegram.ps1 -Development -BuildLabel economy-telemetry-final`.

- Build ID: `2026-09-10_214406589_android_economy-telemetry-final_be7a248f_dirty_fc4bd0d1`.
- Источник: `integration/unity-live`, базовый commit `be7a248f`, `sourceDirty: true` — изменения этой задачи до commit. Все затронутые C# файлы сверены по SHA-256 с sandbox: расхождений 0.
- APK: `Builds/telegram-buffer/2026-09-10_21-44-06-590_integration_unity-live_be7a248f_fc4bd0d1/LostCyberHamster.apk`, 252436567 байт. SHA-256: `dcddc89b20a05f26944eee4f02ad167274751b11ce32ecdbc268ee2a36f86257`.
- `build-summary.codex.json` и manifest рядом с APK. Проверены `economyTelemetryEnabled: true`, `allowOnAndroid: true`, `allowInEditor: false`, `balanceVersion: 2026-09-10-economy-1` в собранном sandbox.

На телефоне пользователь выполняет короткий прогон: первая победа, повтор, Claim квеста, трата, проигрыш/выход, фон, офлайн/перезапуск и возврат сети. Затем агент запускает команду отчёта и сверяет XP/валюты/попытки с ожидаемыми результатами. Показ рекламы/IAP проверяется при доступном провайдере; конфигурация и тестовый режим записываются отдельно.

Фактическая Android-доставка и длинная игровая приёмка пока ожидаются. Desktop-проверка подтверждает код журнала и общий транспорт, но не Android lifecycle. Полная готовность T4 — после этого прогона.
