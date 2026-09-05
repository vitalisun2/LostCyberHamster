# Четыре зоны UI — реализация 05.09.2026

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

Владелец задачи «Расследовать скачки UI при переходах» согласовал этот scope. Его UIManager, ScreenController, PreparedScreen, ScreenLayout и основные menu screen controllers остаются в его владении.

Исходный HEAD: `54cabd92`. Исходный dirty state записан в локальном QA-каталоге `.worktrees/ui-four-zones-qa`. Unity MCP подтвердил ready, Bootstrap, Play stopped, сцена чистая. CLI status вернул пустой список; MCP проверил точный project path и доступный Editor.

## Этапы

- 0. Контракты и владение: независимое review PASS (`a6a5147e`). Cloud DTO/координатор, reward-сервисы, восемь tutorial-шагов сохраняются. Фокус Jump использует актуальный hit area 267×267.
- 1. Графика: PASS. Три новых RGBA PNG, прозрачность и CTA 9-slice проверены. После review исправлены отступ CTA до34px и отсутствующий glyph стрелки в макете. Импортированы6 Cloud,10 Tutorial,3 новых PNG. Общая Win-панель перенесена через AssetDatabase; GUID17be0220a3843a8449538356298591ae сохранён.
- 2. Общая presentation и Cloud: code review PASS после исправления frame snapshot и coordinator busy owner. С offline-задачей интегрированы LaterSelected, SetError, nullable Cloud и локализация. Unity recompile PASS. RU/EN ×4 размера, resize, асимметричная safe area, busy/rebind, nullable Cloud, fallback и события выбора PASS. Runtime Apply повторно возвращает тот же helper; Restore дважды и смена режима PASS. PlayerLevel добавлен пятой строкой.
- 3. Tutorial HUD: code review PASS, Unity recompile PASS. 64 комбинации RU/EN ×4 размера ×8 шагов: текст помещается. 10 действий принимают указатель внутри фокуса и блокируют снаружи; Jump → SuperJump сохраняет геометрию. Gameplay прохождение проверяется вместе с Completion.
- 4. Tutorial Complete: ожидает этапа 3.
- 5. LevelUp: ожидает этапа 4.
- 6. DailyQuestReward: ожидает этапа 5.
- 7. Общее review, visual QA, C# gate: ожидает реализации.

## Проверки

Матрица: RU/EN; 1920×1080, 2340×1080, 2400×1080, 1440×1080; асимметричная safe area; resize; повторное открытие и disable/enable. Игровые данные проверяются изолированно. Unity automation выполняется последовательно под `.worktrees/.integration-lock`.

Финальный C# gate: regeneration, затем build Assembly-CSharp с `--no-restore`.

## Evidence и review

- Art refs: `WorkOnScreens/Level_Up/v03_level_up.png`, `Daily_Quest_Reward/v02_daily_quest_reward.png`.
- Source mapping и PNG QA: `WorkOnScreens/Level_Up/archive/v03_level_up_work/`.
- Baseline captures: `.worktrees/ui-four-zones-qa/baseline-*.png`. Preview использует копии UIDocument/PanelSettings, отдельный RenderTexture и PreviewScene; после capture всё удалено, Bootstrap dirty=false.
- Capture в Unity6000.2 требует `Update → Repaint → Render → ReadPixels`; Repaint только готовит геометрию. Начальные пустые captures заменены после уточнения native Render.
- Helper review: замечание восстановления frame/design при смене useSafeArea закрыто. Cloud review: локализационный fallback добавлен; сброс busy после скрытия передан владельцу offline Coordinator.
