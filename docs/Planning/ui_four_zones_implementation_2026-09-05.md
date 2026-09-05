# Четыре зоны UI — реализация 05.09.2026

## Текущий итог

Все четыре зоны реализованы: Cloud Save Conflict, Tutorial, LevelUp, DailyQuestReward. Финальные code review, visual review и перечисленные runtime-проверки — PASS. Regeneration и C# build: 0 ошибок, 21 предупреждение.

## Договорённости

Cloud Save Conflict, Tutorial, LevelUp, DailyQuestReward. После каждого этапа — независимое review, исправления, проверка затронутого. После всех экранов — полное review кода и визуала.

Tutorial сохраняет завершение до финальной плашки с одной Play. Play запускает первый уровень; Skip сохраняет завершение и сразу запускает уровень.

## Владение

- Интегратор: текущая задача `01a0713c-4434-75d0-aeca-569c8df6d240`, `integration/unity-live`.
- `UI/Modals/GameResultModalPresentation.cs`; controllers CloudSaveConflict, LevelUp, DailyQuestReward; соответствующие UXML/USS.
- `Tutorial/Gameplay`: View, Controller, Step, StepCatalog; `Tutorial/Runtime`: FlowController, Phase, RuntimeHost.
- Новые tutorial/reward USS, минимальный импорт `common.uss`, строки RU/EN.
- Новые PNG cloud/tutorial/rewards, общие panel/CTA и их Unity-generated metadata.
- `UiSafeArea.cs`, `ModalScaleMode.cs`; presentation lifecycle hunks Win/Pause/Journey. Lose аналогичный hunk интегрирует владелец offline ads.
- Отчёт реализации и обновление аудита покрытия UI.
- `GameManagement/Persistence/GameDataManager.cs`: исправление чтения пустого UploadAttempt, выявленное при обязательном перезапуске Tutorial.

Владелец задачи «Расследовать скачки UI при переходах» согласовал этот scope. Его UIManager, ScreenController, PreparedScreen, ScreenLayout и основные menu screen controllers остаются в его владении.

Исходный HEAD: `54cabd92`. Исходный dirty state записан в локальном QA-каталоге `.worktrees/ui-four-zones-qa`. Unity MCP подтвердил ready, Bootstrap, Play stopped, сцена чистая. CLI status вернул пустой список; MCP проверил точный project path и доступный Editor.

## Этапы

- 0. Контракты и владение: независимое review PASS (`a6a5147e`). Cloud DTO/координатор, reward-сервисы, восемь tutorial-шагов сохраняются. Фокус Jump использует актуальный hit area 267×267.
- 1. Графика: PASS. Три новых RGBA PNG, прозрачность и CTA 9-slice проверены. После review исправлены отступ CTA до34px и отсутствующий glyph стрелки в макете. Импортированы6 Cloud,10 Tutorial,3 новых PNG. Общая Win-панель перенесена через AssetDatabase; GUID17be0220a3843a8449538356298591ae сохранён.
- 2. Общая presentation и Cloud: code review PASS после исправления frame snapshot и coordinator busy owner. С offline-задачей интегрированы LaterSelected, SetError, nullable Cloud и локализация. Unity recompile PASS. RU/EN ×4 размера, resize, асимметричная safe area, busy/rebind, nullable Cloud, fallback и события выбора PASS. Runtime Apply повторно возвращает тот же helper; Restore дважды и смена режима PASS. PlayerLevel добавлен пятой строкой.
- 3. Tutorial HUD: code review и Unity recompile PASS. 64 комбинации RU/EN ×4 размера ×8 шагов: текст помещается. 10 действий принимают указатель внутри фокуса и блокируют снаружи; Jump/SuperJump сохраняет геометрию. Реальное прохождение подтверждено на этапе 4.
- 4. Tutorial Complete: source, visual и runtime PASS. 16 preview RU/EN ×4 размера ×success/error, overflow 0. Пользователь прошёл 8 шагов и остановил Play Mode; сохранены completed=true и баланс 731. Ошибка при missing backup показала Retry; восстановление защищённого backup и Retry дали success UI. Расшифрованное сохранение до Play: completed=true, Money=731; одна Play, InputBlocked=true. UIDocument disable/enable и production repaint создали новый View при прежнем Controller, сохранили Completion. Late Skip + Double Play: sceneLoads=1, hostGone=true, InputBlocked=false, первый уровень.
- 5. LevelUp: независимый visual PASS, 8 финальных кадров RU/EN ×4 размера. RU: «ОЧКИ РАЗВИТИЯ: +2». Три вызова обработчика дали close=1 и callback=1.
- 6. DailyQuestReward: независимый visual и runtime PASS. Заголовок исправлен: top=207, height=128, measuredHeight=128; 8 кадров RU/EN ×4 размера. FIFO: баланс 731/29; три вызова с rebind дали 761/29, очередь 1, close 1. Следующие 40 кристаллов дали 761/69, очередь 0, close 2. Отказ при пустой очереди сохранил данные. Максимальное значение 2147483647 и иконка кристаллов читаются.
- 7. Финальные code/visual review: PASS. Дополнительное независимое review исправлений storage и teardown: PASS. Regeneration и C# build: PASS.

## Проверки

Матрица: RU/EN; 1920×1080, 2340×1080, 2400×1080, 1440×1080; асимметричная safe area; resize; повторное открытие и disable/enable. Игровые данные проверяются изолированно. Unity automation выполняется последовательно под `.worktrees/.integration-lock`.

Финальный C# gate 05.09.2026: `lch_project_regenerate_files`, затем `dotnet build Assembly-CSharp.csproj --no-restore`; exit 0, 0 ошибок, 21 предупреждение существующего проекта.

Preview-матрицы дополнены реальным прохождением и проверками состояния в Editor. Перезапуск — Stop/Start Play Mode с повторной загрузкой Bootstrap и сохранения; запуск отдельного приложения на устройстве в этот прогон не входил. Ошибка сохранения проверена через отсутствие защищённого tutorial backup и повторное действие, физический отказ диска не воспроизводился. Обработчики LevelUp/Daily и повторных переходов вызывались напрямую на реальных контроллерах с production-сервисами.

На финальном review исправлены два дефекта. JsonUtility превращает null UploadAttempt в объект с пустыми полями; загрузчик теперь распознаёт только полностью пустой объект как отсутствие попытки. Валидный объект принимается, частичный отклоняется. Teardown Tutorial освобождает UI без Resume уже уничтоженного мира; Host гарантирует освобождение блокировок.

Повторная проверка: завершение сохранено до Play; Stop/Start Play восстанавливает completed=true, Money=731, Crystals=29, первый уровень. Skip с повторным вызовом сохраняет завершение и освобождает guards. Исходные PlayerPrefs пользователя побайтно совпадают с резервной копией; productName восстановлен, Bootstrap чистая, Play остановлен.

## Артефакты и review

| Проверка | Результат и артефакты |
|---|---|
| Cloud | 8 финальных кадров, RU/EN ×4 размера. [RU1920][cloud-ru], [EN1440][cloud-en]. Runtime выбор, busy/rebind, nullable Cloud, resize и safe area PASS на этапе2. |
| Tutorial HUD | [Матрица64 проверок][hud-matrix], overflow0. [RU шаг4][hud-ru], [EN шаг8][hud-en]. 10 действий: внутри принимаются, снаружи блокируются; непрерывность Jump/SuperJump PASS. |
| Tutorial Complete | [Матрица 16 preview][complete-matrix], overflow 0; [RU success][complete-ru], [EN success][complete-en], [RU error][complete-error-ru], [EN error][complete-error-en]. [Runtime-результат][completion-runtime]: сохранение до Play, rebind, одна загрузка сцены, guards освобождены. |
| LevelUp | 8 финальных captures; [RU1920][levelup-ru], [EN1440][levelup-en]. Независимый visual PASS. Повторный обработчик: 3 вызова, close 1, callback 1. |
| DailyQuestReward | 8 кадров; [RU1920][daily-ru], [EN1440][daily-en]. [Runtime-результат][daily-runtime]: height/measuredHeight 128, две FIFO-награды без повторного начисления, отказ без изменений. |
| Перезапуск и storage | [Результат перезапуска][restart]; [пустой, валидный и частичный UploadAttempt][envelope]. |

![Финал обучения в Unity](C:/Personal/crystal-wave/repos/LostCyberHamster_2025/.worktrees/ui-four-zones-qa/tutorial-runtime-completion.png)

![Общая награда Daily](C:/Personal/crystal-wave/repos/LostCyberHamster_2025/.worktrees/ui-four-zones-qa/daily-ru-1920.png)


- Art refs: `WorkOnScreens/Level_Up/v03_level_up.png`, `Daily_Quest_Reward/v02_daily_quest_reward.png`.
- Source mapping и PNG QA: `WorkOnScreens/Level_Up/archive/v03_level_up_work/`.
- Baseline captures: `.worktrees/ui-four-zones-qa/baseline-*.png`. Preview использует копии UIDocument/PanelSettings, отдельный RenderTexture и PreviewScene; после capture всё удалено, Bootstrap dirty=false.
- Capture в Unity6000.2 требует `Update → Repaint → Render → ReadPixels`; Repaint только готовит геометрию. Начальные пустые captures заменены после уточнения native Render.
- Helper review: замечание восстановления frame/design при смене useSafeArea закрыто. Cloud review: локализационный fallback добавлен; сброс busy после скрытия передан владельцу offline Coordinator.

[cloud-ru]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/.worktrees/ui-four-zones-qa/cloud-ru-1920-final.png
[cloud-en]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/.worktrees/ui-four-zones-qa/cloud-en-1440-final.png
[hud-matrix]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/.worktrees/ui-four-zones-qa/tutorial-hud-matrix.json
[hud-ru]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/.worktrees/ui-four-zones-qa/tutorial-ru-step4-1920.png
[hud-en]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/.worktrees/ui-four-zones-qa/tutorial-en-step8-1440.png
[complete-matrix]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/.worktrees/ui-four-zones-qa/tutorial-complete-matrix.json
[complete-ru]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/.worktrees/ui-four-zones-qa/tutorial-complete-ru-1920.png
[complete-en]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/.worktrees/ui-four-zones-qa/tutorial-complete-en-1440.png
[complete-error-ru]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/.worktrees/ui-four-zones-qa/tutorial-complete-ru-error-1920.png
[complete-error-en]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/.worktrees/ui-four-zones-qa/tutorial-complete-en-error-1440.png
[levelup-ru]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/.worktrees/ui-four-zones-qa/levelup-ru-1920.png
[levelup-en]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/.worktrees/ui-four-zones-qa/levelup-en-1440.png
[daily-ru]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/.worktrees/ui-four-zones-qa/daily-ru-1920.png

[completion-runtime]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/.worktrees/ui-four-zones-qa/tutorial-completion-runtime.json
[daily-runtime]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/.worktrees/ui-four-zones-qa/daily-claim-results.json
[daily-en]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/.worktrees/ui-four-zones-qa/daily-en-1440.png
[restart]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/.worktrees/ui-four-zones-qa/tutorial-restart-before-play.json
[envelope]: C:/Personal/crystal-wave/repos/LostCyberHamster_2025/.worktrees/ui-four-zones-qa/persistence-envelope-results.json
