# AI Workflow Lessons

Накопленные уроки по эффективности работы в этом репозитории. Не архитектура и не спецификация — только практические выводы, ускоряющие следующие задачи.

## Как использовать

- Читать перед существенной задачей, особенно связанной с ботом, логами, тестовыми уровнями или Unity Editor workflow.
- После завершения задачи добавлять только устойчивые выводы, которые повышают качество следующих задач.
- Не превращать файл в дневник. Добавлять краткие правила, а не историю действий.

### Статусы lessons

- `incubating` — наблюдение полезно, но ещё не закреплено как правило.
- `promoted` — lesson перенесён в файл-владелец темы в `docs/rules/`.

Формат пометки в списке:

- `[incubating] ...`
- `[promoted -> docs/rules/<owner-file>.md] ...`

## Уроки

### Promoted

#### Диагностика и отладка бота

- [promoted -> docs/rules/iteration_cycle.md] Отделять проблему поведения от проблемы логирования.
- [promoted -> docs/rules/iteration_cycle.md] Локализовать 1 провал в логе перед глубокой правкой.
- [promoted -> docs/rules/iteration_cycle.md] Визуальные багрепорты: lane хомяка, 2-3 obstacle type, момент уровня.
- [promoted -> docs/architecture_knowledge_base.md] HamsterState — источник истины для прыжков.
- [promoted -> docs/rules/iteration_cycle.md] Логировать start/end/net + collected/spent, не только WIN/FAIL.
- [promoted -> docs/rules/agent_tools.md] Читать diagnostic_log.txt по каналам: STAB → BOT → ECO.
- [promoted -> docs/rules/agent_tools.md] Читать логи через read_log_channel.ps1 для унификации между агентами.

#### Семантика бота

- [promoted -> docs/architecture_knowledge_base.md] Семантика safe, SwitchLane windows, ThreatSafety target, chain-этапы, ObjectCategory vs runtime-dangerous set.

#### Тестирование

- [promoted -> docs/rules/iteration_cycle.md] Собирать компактный representative test_level с distinct-механиками.
- [promoted -> docs/rules/agent_tools.md] При поиске по EditorLogs учитывать игнорируемые пути (includeIgnoredFiles=true).
- [promoted -> docs/rules/agent_tools.md] Если automation bridge без [TEST RESULT] и лог пустой — читать Unity Editor.log.
- [promoted -> docs/architecture_knowledge_base.md] Для chain-stage тестов все объекты цепочки должны попадать в initial snapshot.
- [promoted -> docs/rules/iteration_cycle.md] Для chain-планирования читать BOT вместе с ECO.
- [promoted -> docs/rules/code_conventions.md] EditMode-тесты для planner держать тонким слоем и не дублировать ими runtime-проверку на test level.

#### Архитектура ActionGenerator

- [promoted -> docs/architecture_knowledge_base.md] Ограничения ActionGenerator, IsSwitchLaneSafeAtDistance, TryComputeSwitchLaneExecuteDistance.

#### Изучение runtime перед реализацией

- [promoted -> docs/rules/code_conventions.md] Перед реализацией игровой механики изучить runtime: коллизии, state transitions, animation events.
- [promoted -> docs/architecture_knowledge_base.md] Пример: SwitchLaneSafety и source-phase check (IsOnBottomLine + TapRequest).

#### Архитектурная чистота vs костыли

- [promoted -> docs/architecture_knowledge_base.md] Не добавлять фильтры по типу, planning vs execution проверки, swept zone.

#### Workflow и правила

- [promoted -> docs/rules/AGENTS.md] Отвечать максимально кратко; подробности — по запросу.
- [promoted -> docs/rules/AGENTS.md] Перед задачей читать AGENTS.md и релевантные правила.
- [promoted -> docs/rules/workflow.md] Побочные изменения (автогенерация) должны попадать в main.
- [promoted -> docs/rules/workflow.md] При валидации запускать только релевантный тестовый уровень.
- [promoted -> docs/rules/workflow.md] Не объявлять задачу завершённой до конца git-цикла.
- [promoted -> docs/rules/code_conventions.md] При удалении файлов проверять тесты на ссылки на удалённые типы.
- [promoted -> docs/rules/code_conventions.md] После изменений .cs файлов запускать recompile_scripts.

#### Эффективность итераций

- [promoted -> docs/rules/code_conventions.md] Не делать больше 2 попыток фикса без изучения runtime.
- [promoted -> docs/rules/code_conventions.md] При изменении физических констант сначала проверить использование в game engine.

### Incubating

#### Данные и миграция

- [incubating] При добавлении обязательных полей в JSON-данные мигрировать существующие файлы ДО деплоя кода. `JsonUtility.FromJson` подставляет `default(int) = 0` для отсутствующих полей.
