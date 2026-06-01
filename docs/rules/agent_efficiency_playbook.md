# Agent Efficiency Playbook

Единый владелец уроков о том, как AI-агентам быстрее, точнее и дешевле работать в этом репозитории.

Этот документ не хранит архитектуру, gameplay-спецификации и дневник задач. Если lesson стал правилом конкретной области, он переносится в файл-владелец; здесь остаётся только краткая promoted-ссылка.

## Когда читать

- Перед любой существенной аналитической или исполнительной задачей.
- После compaction/resume, если есть риск, что обязательное входное чтение потеряно.

## Learning Review в конце каждой задачи

Выполняется после завершения содержательной работы и локальной валидации, но до финального ответа пользователю. Подтверждение пользователя не требуется.

1. Сформулировать короткий итог задачи: что сделано, чем проверено, что осталось на ручную проверку.
2. Проверить собственный ход работы:
   - был ли выбран неверный или слишком широкий инструмент;
   - было ли лишнее чтение, полный diff вместо targeted checks или шумный вывод;
   - были ли повторные failed patch / validation / fix attempts;
   - были ли упущены обязательные правила, владелец файла, dirty workspace или untracked files;
   - можно ли было раньше сузить гипотезу по коду, логам или runtime trace.
3. Если найден новый reusable lesson, добавить его ниже:
   - `incubating` — наблюдение полезно, но ещё не закреплено как правило;
   - `promoted` — устойчивое правило перенесено в файл-владелец.
4. Если нового lesson нет, ничего не добавлять и написать в финальном отчёте: `Learning Review Done: новых lessons нет`.

## Формат новых записей

### Incubating

```text
- [incubating] Ошибка/риск: ... Причина: ... Рабочий ход: ... Правило: ...
```

- [incubating] Ошибка/риск: runtime verbose-диагностика Unity Play Mode не попадает в лог после включения static flag из Editor automation. Причина: domain reload может сбросить runtime static state перед стартом Play Mode. Рабочий ход: для обязательных bot-test логов использовать forced `DiagLog` в runtime path или включать verbose из runtime-кода после загрузки сцены. Правило: не полагаться на Editor-side static flags для Play Mode диагностики без проверки фактического лога.

### Promoted

```text
- [promoted -> <owner-file>] <короткое правило>
```

Правила:

- Обобщать до уровня проекта, не описывать историю конкретной задачи.
- Не добавлять запись, если она повторяет существующее правило.
- Если lesson относится к коду, workflow, инструментам, бот-итерациям или архитектуре, переносить его в соответствующий owner file, а здесь оставлять только promoted-ссылку.
- Удалять или переписывать устаревшие lessons вместо сохранения противоречий.

## Устойчивые правила эффективности

### Входное чтение

- До первого patch прочитать `AGENTS.md`, этот playbook, `workflow.md`, `code_conventions.md`, `temporary_current_rules.md` и релевантные owner files из секции входного чтения.
- После compaction/resume быстро проверить, какие обязательные файлы уже прочитаны, прежде чем продолжать правки.

### Узкий контекст перед широким

- Начинать диагностику с конкретного провала, файла, лога или pathspec; расширять поиск только когда узкий контекст не объясняет проблему.
- Полный `git diff` или лог читать только после `--stat`, targeted `status`, channel filter или другого компактного среза.

### Патчи и рефакторинг

- Делить manual patch по слоям ответственности: predicate/helper, call sites, tests/specs, cleanup.
- После failed patch уменьшать scope, а не повторять тот же большой diff.
- После введения центрального helper/predicate искать старые primitive checks, прямые обходные вызовы и obsolete usings.
- После helper extraction проверять, не появились ли повторные проходы по одной коллекции; факты для одного решения собирать одним scan.
- [incubating] Ошибка/риск: manual `apply_patch` в задаче с отдельным worktree может попасть в основной каталог. Причина: tool не принимает `workdir` и резолвит пути от текущего workspace root. Рабочий ход: для task-worktree patch указывать path с префиксом `.worktrees/<slug>/...`, затем проверять `git status` в основном каталоге и worktree. Правило: после создания worktree первый manual patch применять только с явным worktree-prefixed filename.

### Проверка execution path

- Перед изменением helper/predicate подтвердить его call site и участие в активном execution path.
- Если меняется смысл target/trigger/support у action/event, проверить generic gates: revalidators, executors, simulations, subscribers.
- Не ослаблять общие planner/runtime инварианты, пока trace не доказал, что invariant неверен.

### Работа с инструментами

- В dirty workspace валидировать targeted pathspecs текущей задачи и не чинить unrelated changes.
- Если целевой файл `??`, `git diff` недостаточен: нужны targeted `git status` и прямое чтение содержимого.
- Если summary подагента или инструмента противоречив, добрать короткий raw command с точным статусом.
- На Windows явно использовать native paths (`C:\...`) в PowerShell-командах и инструкциях подагентам.
- Для Unity diagnostic logs брать фактический путь из automation output/DebugManager, а не угадывать по имени файла.
- [incubating] Ошибка/риск: `EditorWindow`-кнопка перестаёт выполнять действие после входа в Play Mode. Причина: делегаты и несериализуемое состояние окна не переживают Unity domain reload. Рабочий ход: хранить только восстанавливаемые данные окна, вызывать статические команды напрямую или восстанавливать callback в `OnEnable`. Правило: editor tools, которые должны работать во время Play Mode, не должны зависеть от сохранённого `Action` в окне.
- При удалении или объединении rule-файла выполнить `rg` по старому имени в `docs`, `AGENTS.md`, `CLAUDE.md` и `.github`, затем обновить все agent entrypoints до финальной проверки.

### Runtime-зависимые правки

- Для collision/timing/window logic проверять lifecycle событий (`Enter`, `Stay`, execution window), а не только первый видимый callback.
- Для targeted gameplay event сначала классифицировать его как command или fact, затем проверять publisher/subscriber side effects на дублирование.

## Promotion Registry

- [promoted -> docs/rules/AGENTS.md] Краткий стиль ответа, обязательное входное чтение и Learning Review в конце каждой задачи.
- [promoted -> docs/rules/workflow.md] Git/workflow-цикл, targeted validation, отчётность и запрет объявлять задачу завершённой до выбранной точки workflow.
- [promoted -> docs/rules/code_conventions.md] Runtime-first подход к gameplay-механикам, ограничения blind fixes, Unity `.meta`/`.csproj`, тонкий слой EditMode-тестов и JSON data migration.
- [promoted -> docs/rules/iteration_cycle.md] Бот-итерации: отделять поведение от логирования, локализовать один провал, использовать visual feedback, representative levels, читать BOT вместе с ECO.
- [promoted -> docs/rules/agent_tools.md] Automation bridge, diagnostic logs, `read_log_channel.ps1`, ignored paths, Editor.log fallback, Unity wake-up и full bot validation.
- [promoted -> docs/architecture_knowledge_base.md] Устойчивые bot/runtime выводы: HamsterState, SwitchLane windows, ThreatSafety target, chain semantics, ActionGenerator инварианты.
- [promoted -> docs/architecture_knowledge_base.md] Bot planning: для timed jump-on objective сохранять temporal slack первого действия, иначе поздний prefix может разрушить окно retained jump-on.
- [promoted -> docs/architecture_knowledge_base.md] Bot planning: во время in-progress head-action стабилизировать только атомарный execution handoff, а не весь дальний хвост плана.
