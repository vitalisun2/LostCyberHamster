# Agent Efficiency Playbook

Единый владелец обязательного fast operating protocol и lessons о том, как AI-агентам быстрее, точнее и дешевле работать в этом репозитории.

Этот документ не хранит архитектуру, gameplay-спецификации и дневник задач. Если lesson стал правилом конкретной области, он переносится в файл-владелец; здесь остаётся только краткая promoted-ссылка.

## Когда читать

- Перед началом любой задачи: прочитать operating protocol и проверить накопленные lessons на релевантность текущей работе.
- По явному запросу пользователя на эффективность или правила работы агента.
- Если fast path не объясняет задачу и нужен расширенный анализ процесса.
- После compaction/resume, если есть риск, что обязательное входное чтение потеряно.

## Обязательный fast operating protocol

- Сначала зафиксировать минимальный набор смысловых алгоритмов и дождаться approval; реализовывать только текущий одобренный алгоритм по KISS, без архитектуры на будущее.
- Planning-only работа не меняет файлы и не запускает compile, Unity или runtime validation.
- В implementation-задаче после узкого fast-path быстро довести минимальный кусок до code-ready и сразу проверить затронутый diff; не растягивать upfront-анализ без нового evidence.
- Параллельно вести только независимые workstreams. Один implementation-agent отвечает за свой кусок end-to-end; отдельный validation-agent подключается после code-ready и не конкурирует за те же файлы.
- Сразу после минимальной реализации, до продолжения validation, отправить точное сообщение: `реализация готова, валидация продолжается`.
- Unity automation давать 30-60 секунд. При timeout сразу проверить resolved project root, request/response paths и `Editor.log`; не повторять слепо ту же команду.
- Validation запускать по одной команде; результат и точный evidence сообщать сразу, прежде чем переходить к следующей проверке.
- При первом фактическом сигнале к рефакторингу остановить затронутую реализацию, сформулировать конкретное предложение и дождаться обязательного approval; до approval не рефакторить.
- В рабочем контексте держать компактный task-state: цель, текущий approved stage, что завершено, активные агенты, validation evidence, blockers и следующий шаг.
- Новый process lesson добавлять только после проверки на дубли в формате `Симптом: ... Причина: ... Правильное действие: ...`; технические coding, validation и worktree-правила не переносить из их owner-файлов.

## Формат новых записей

### Incubating

```text
- [incubating] Симптом: ... Причина: ... Правильное действие: ...
```

- [incubating] Симптом: runtime verbose-диагностика Unity Play Mode не попадает в лог после включения static flag из Editor automation. Причина: domain reload может сбросить runtime static state перед стартом Play Mode. Правильное действие: для обязательных bot-test логов использовать `BotDiagnostics`/профильный diagnostics-helper на runtime path с нужным `BotDiagnosticLevel` или включать verbose из runtime-кода после загрузки сцены; `DebugManager` оставлять только sink и проверять фактический лог.

### Promoted

```text
- [promoted -> <owner-file>] <короткое правило>
```

Правила:

- Обобщать до уровня проекта, не описывать историю конкретной задачи.
- Не добавлять запись, если она повторяет существующее правило.
- Если lesson относится к коду, инструментам, бот-итерациям или архитектуре, переносить его в соответствующий owner file, а здесь оставлять только promoted-ссылку.
- Удалять или переписывать устаревшие lessons вместо сохранения противоречий.

## Устойчивые правила эффективности

### Входное чтение

- Для fast code-edit задач читать только целевой код, соседние call sites и ближайший execution path; rule files, docs, историю и generated snapshots подключать только при явной необходимости.
- После compaction/resume быстро проверить, какие обязательные файлы уже прочитаны, прежде чем продолжать правки.

### Узкий контекст перед широким

- Начинать диагностику с конкретного провала, файла, лога или pathspec; расширять поиск только когда узкий контекст не объясняет проблему.
- Полный `git diff`, историю или широкий `rg` запускать только когда узкий контекст не объясняет проблему или пользователь прямо просит расширенную проверку.

### Патчи и рефакторинг

- Делить manual patch по слоям ответственности: predicate/helper, call sites, specs, cleanup.
- После failed patch уменьшать scope, а не повторять тот же большой diff.
- После введения центрального helper/predicate искать старые primitive checks, прямые обходные вызовы и obsolete usings.
- После helper extraction проверять, не появились ли повторные проходы по одной коллекции; факты для одного решения собирать одним scan.

### Проверка execution path

- Перед изменением helper/predicate подтвердить его call site и участие в активном execution path.
- Если меняется смысл target/trigger/support у action/event, проверить generic gates: revalidators, executors, simulations, subscribers.
- Не ослаблять общие planner/runtime инварианты, пока trace не доказал, что invariant неверен.

### Работа с инструментами

- В dirty workspace валидировать targeted pathspecs текущей задачи и не чинить unrelated changes.
- Если целевой файл `??`, `git diff` недостаточен: нужны targeted `git status` и прямое чтение содержимого.
- Если summary подагента или инструмента противоречив, добрать короткий raw command с точным статусом.
- На Windows явно использовать native paths (`C:\...`) в PowerShell-командах и инструкциях подагентам.
- [incubating] Симптом: ручное восстановление CRLF/LF идёт против `.gitattributes` и сохраняет предупреждения Git. Причина: видимый старый формат строк не всегда равен требуемому `attr/text eol`. Правильное действие: сначала выполнить `git ls-files --eol <path>` и привести рабочую копию к указанному `w/...`/`attr` формату; не менять line endings на глаз.
- Для Unity diagnostic logs брать фактический путь из automation output/`DebugManager.GetDiagLogPath()`, а не угадывать по имени файла.
- [incubating] Симптом: `EditorWindow`-кнопка перестаёт выполнять действие после входа в Play Mode. Причина: делегаты и несериализуемое состояние окна не переживают Unity domain reload. Правильное действие: хранить только восстанавливаемые данные окна, вызывать статические команды напрямую или восстанавливать callback в `OnEnable`; Play Mode tools не должны зависеть от сохранённого `Action`.
- При удалении или объединении rule-файла выполнить `rg` по старому имени в `docs`, `AGENTS.md`, `CLAUDE.md` и `.github`, затем обновить все agent entrypoints до финальной проверки.

### Runtime-зависимые правки

- Для collision/timing/window logic проверять lifecycle событий (`Enter`, `Stay`, execution window), а не только первый видимый callback.
- Для targeted gameplay event сначала классифицировать его как command или fact, затем проверять publisher/subscriber side effects на дублирование.

## Promotion Registry

- [promoted -> docs/rules/AGENTS.md] Краткий стиль ответа, fast code-edit workflow и минимальное входное чтение.
- [promoted -> docs/rules/code_conventions.md] Runtime-first подход к gameplay-механикам, ограничения blind fixes, Unity `.meta`/`.csproj`, запрет инициативного добавления тестов и JSON data migration.
- [promoted -> docs/rules/agent_tools.md] Automation bridge, diagnostic logs, `tools/read_log_channel.ps1`, ignored paths, Editor.log fallback, Unity wake-up и full bot validation.
- [promoted -> docs/rules/agent_tools.md] Test-level validation: `WIN`/`FAIL`/звёзды не являются критерием регрессии; сверять фактические actions с `description` test-паттернов.
- [promoted -> docs/rules/agent_tools.md] При "зависшем" Unity automation сначала проверить, что request/response paths указывают на реальный Unity project root.
- [promoted -> docs/architecture_knowledge_base.md] Устойчивые bot/runtime выводы: HamsterState, SwitchLane windows, ThreatSafety target, chain semantics, ActionGenerator инварианты.
- [promoted -> docs/architecture_knowledge_base.md] Bot planning: для timed jump-on objective сохранять temporal slack первого действия, иначе поздний prefix может разрушить окно retained jump-on.
- [promoted -> docs/architecture_knowledge_base.md] Bot planning: для target-bound jump-on выбирать fire shift с учетом post-action safety, а не отбрасывать весь action по первому runtime-valid timing.
- [promoted -> docs/architecture_knowledge_base.md] Bot planning: во время in-progress head-action стабилизировать только атомарный execution handoff, а не весь дальний хвост плана.
- [promoted -> docs/architecture_knowledge_base.md] Bot validation: planner diagnosis не равна доказанному level dead-end; подтверждать dead-end только через runtime `LivesLost`.
- [promoted -> docs/architecture_knowledge_base.md] Bot planning: dead-end report на глубине N описывает первый zero-candidate узел; dead-end branch fallback должен сохранять безопасный prefix для level validation.
- [promoted -> docs/architecture_knowledge_base.md] SwitchLane planning: после fire учитывать runtime-блокировку следующего tap до конца `Hamster.IsShifting`.
- [promoted -> docs/architecture_knowledge_base.md] Bot execution: retained next action должен получать immediate handoff перед rebuild, а actions с ранним runtime handoff нельзя ждать только до финального `Run`.
- [promoted -> docs/architecture_knowledge_base.md] Bot validation: нехватка энергии у применимой jump-стратегии должна оставаться dead-end diagnosis, а не уходить в `NotApplicable`.
