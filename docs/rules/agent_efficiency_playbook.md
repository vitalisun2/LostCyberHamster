# Agent Efficiency Playbook

Практические правила, которые помогают агенту работать быстрее, точнее и дешевле по токенам. Это не архитектура проекта и не gameplay-спецификация: сюда попадают рабочие приёмы, родившиеся из неудачных попыток.

## Как использовать

- Читать перед существенной аналитической или исполнительной задачей вместе с `ai_workflow_lessons.md`.
- После завершения аналитической или исполнительной задачи обязательно выполнить efficiency review: найти, где работа была медленной, ошибочной или шумной, и при наличии нового паттерна дополнить этот документ.
- Добавлять только опыт формата: неудачная попытка -> причина -> удачный способ -> правило на будущее.
- Не превращать документ в лог сессии. Описывать устойчивый рабочий паттерн, а не всю историю действий.
- Не добавлять запись, если она только повторяет уже существующее правило; в таком случае отметить в отчёте, что новых efficiency lessons нет.
- Если правило стало неактуальным, удалить или переписать его, а не оставлять противоречащие советы.

## Формат записи

```text
### Короткое название

- Контекст: где проявилось.
- Неудачный ход: что агент сделал и почему это оказалось медленно/ошибочно.
- Рабочий ход: что сработало.
- Правило: как действовать в следующий раз.
```

## Правила из текущего опыта

### Разбивать patch по смысловым слоям

- Контекст: правка roof occupant semantics в bot strategies.
- Неудачный ход: первый patch объединил новый predicate, замену call sites, constructor changes и cleanup. `apply_patch` не применился из-за порядка hunks.
- Рабочий ход: разделить изменения на маленькие шаги: сначала доменный predicate, затем detector/calculator, затем specifications, затем retained validator и cleanup.
- Правило: manual patch не должен смешивать несколько смысловых операций. Для multi-file изменения применять патчи по слоям ответственности, а после сбоя не повторять большой patch, а уменьшать scope.

### Использовать Windows-native paths в подагентах

- Контекст: сбор diff/status через execution subagent на Windows.
- Неудачный ход: команда с `/c/Personal/...` не нашла репозиторий и потратила попытку.
- Рабочий ход: использовать PowerShell-форму `Set-Location 'C:\Personal\...'` или `git -C 'C:\Personal\...'`.
- Правило: на Windows явно писать подагенту PowerShell-команды с native paths. Не использовать Unix-style `/c/...`, если shell не подтверждён как Git Bash/MSYS.

### Ограничивать вывод diff перед чтением

- Контекст: повторный сбор полного diff вернул слишком длинный ответ.
- Неудачный ход: просить полный `git diff` по нескольким файлам через subagent, когда нужен был только контроль состояния.
- Рабочий ход: сначала `git diff --stat`, `git diff --check`, `git status --short -- <target paths>`, а полный diff читать только точечно.
- Правило: для проверки результата начинать с компактных команд. Полный diff запрашивать только по одному файлу или узкому смысловому блоку.

### Отделять свои изменения от грязного workspace

- Контекст: общий `git diff --check` показал десятки unrelated Unity/meta изменений.
- Неудачный ход: глобальная проверка смешала целевую правку с уже существующим шумом workspace.
- Рабочий ход: повторить status/check только по списку файлов текущей задачи и отдельно отметить unrelated changes.
- Правило: в dirty workspace всегда проверять targeted pathspecs перед выводами. Не чинить и не форматировать чужие изменения без явной команды.

### Читать обязательные правила до реализации

- Контекст: часть обязательных документов (`AGENTS.md`, coding rules) была дочитана уже после основной правки.
- Неудачный ход: реализация началась после workflow и исходников, но до полного набора mandatory rules.
- Рабочий ход: дочитать правила и проверить, не требуют ли они корректировки diff.
- Правило: перед существенной исполнительной задачей читать `AGENTS.md`, `workflow.md`, `ai_workflow_lessons.md`, этот playbook и релевантные rules до первого patch. После compaction быстро перепроверять, что mandatory reads не потерялись.

### После централизации искать устаревшие локальные проверки

- Контекст: после добавления `TryFindDamagingOccupantOnPassiveRoofPath` остался старый type/lane filter в `RoofJumpOverSpecification`.
- Неудачный ход: первоначально заменить только очевидные call sites, не проверив все места с тем же primitive condition.
- Рабочий ход: выполнить targeted search по старому признаку (`smallNotAliveRoadAndRoof`) и прямому helper call, затем перевести оставшийся specification на новый predicate.
- Правило: если вводится центральный predicate/helper, сразу искать старые primitive checks и прямые обходные вызовы. Удалять obsolete usings, locals и helpers в том же проходе.

### Проверять неоднозначный summary подагента точным коротким command

- Контекст: subagent summary противоречиво описал `RoofOccupantHazardDetector.cs` как modified/untracked.
- Неудачный ход: полагаться на пересказ, когда важен точный git status.
- Рабочий ход: выполнить короткий targeted `git status --short -- <paths>` и прочитать raw output.
- Правило: если summary инструмента противоречив или слишком агрегирован, добрать точный короткий вывод. Команда должна быть read-only, узкой и с малым объёмом результата.

### Проверять Unity script ownership, если файл untracked

- Контекст: затронутый `RoofOccupantHazardDetector.cs` оказался untracked, а `.meta` тоже untracked, хотя `Assembly-CSharp.csproj` уже содержит `Compile Include`.
- Неудачный ход: можно было пропустить статус файла и думать, что это обычная tracked-правка.
- Рабочий ход: проверить csproj entry и отдельно сообщить пользователю о статусе untracked, не создавая `.meta` вручную.
- Правило: при изменении Unity script, который git считает untracked, проверить `.meta` и `.csproj`. Не писать `.meta` руками; если `.meta` уже сгенерирована Unity, только зафиксировать её статус в отчёте.

### Не полагаться на git diff для untracked целевых файлов

- Контекст: правка rules затронула `clean_code_by_b_martin/README.md`, который был `??` и не попал в `git diff --check`.
- Неудачный ход: считать узкую валидацию завершённой после `git diff --check -- <files>` и `git diff --stat -- <files>`.
- Рабочий ход: добрать `git status --short -- <files>` и прямое чтение untracked файла, чтобы подтвердить его статус и содержимое.
- Правило: если среди целевых pathspec есть `??`, `git diff` недостаточен для финальной проверки. Всегда добавлять targeted `git status` и прямую проверку содержимого untracked файла.

### Проверять Enter/Stay при overlap-порогах

- Контекст: добавление damage по значимому X-overlap в `CollisionController`.
- Неудачный ход: сначала повесить порог только на `OnTriggerEnter2D`, где первый контакт может быть тоньше требуемого порога.
- Рабочий ход: проверить жизненный цикл Unity trigger-событий и добавить обработку `OnTriggerStay2D` для состояний, где overlap может вырасти после входа.
- Правило: если новая collision-логика зависит от величины overlap, сразу проверять, достаточно ли `Enter`, или нужен `Stay` для повторной оценки порога.

### Проверять, что helper подключён к активному path

- Контекст: точечная правка `CanDamageInJumpState` в `CollisionController`.
- Неудачный ход: можно сосредоточиться на незавершённом helper-методе и не заметить, что `ProcessTrigerEnter` его вообще не вызывает.
- Рабочий ход: перед patch сделать короткий usage-check и, если method unused, сразу подключить его к controlling branch.
- Правило: когда правка делается внутри helper/predicate, сначала подтвердить его call site. Не дописывать изолированный метод, если он не участвует в реальном execution path.

### Проверять generic gates после смены target semantics

- Контекст: refactor `JumpFromRoofOnRoof` перевёл action target с blocker/gap model на target roof.
- Неудачный ход: сначала обновить strategy/finder/validator и пропустить общий `RetainedActionRevalidator`, где target обязан был быть внутри текущей decision chain.
- Рабочий ход: после смены значения `TargetObstacle` найти все generic gates вокруг retained/execution path и добавить узкое исключение только для roof-to-roof.
- Правило: если меняется смысл target/trigger/support у `PlannedAction`, сразу проверять общие revalidator/executor/simulation gates, а не только strategy-local code.

### Классифицировать targeted event как command или fact

- Контекст: перевод targetless `DestroyObstacleEvent` на `AtomicEvent<Obstacle>` при сохранении passive obstacle listeners.
- Неудачный ход: можно передавать target как факт после прямого unspawn и одновременно оставить obstacle listener, получив duplicate-unspawn/boom.
- Рабочий ход: перед patch явно решить, событие является командой уничтожить target или фактом после уничтожения, и привести все источники к одной модели.
- Правило: при добавлении payload в gameplay event сначала классифицировать event как command или fact, затем проверять все publisher/subscriber side effects на дублирование.

### Проверять multiplicity обходов после helper extraction

- Контекст: refactor `JumpFromRoofOnRoofFireWindowFinder` разделил поиск blocker и target roof на отдельные helpers.
- Неудачный ход: каждый helper заново обходил один и тот же хвост obstacle list после `lastRoofIndex`, хотя оба факта нужны одному сценарию.
- Рабочий ход: схлопнуть поиск в один проход, который одновременно подтверждает run-from-roof blocker и запоминает следующую roof-цель.
- Правило: после выноса private helpers для одного алгоритма проверить, не размножились ли одинаковые проходы по одной коллекции. Если факты нужны одному решению, собирать их одним scan-ом.

### Читать diagnostic log по пути DebugManager

- Контекст: чтение Unity diagnostic logs после `invoke_open_unity_test_level.ps1`.
- Неудачный ход: искать `diagnostic_log.txt` в корневом `EditorLogs/`, хотя `DebugManager` в Editor пишет в `LostCyberHamster/EditorLogs/` через `Application.dataPath/../EditorLogs`.
- Рабочий ход: брать путь из вывода automation script или из `DebugManager.GetDiagLogPath()`, затем фильтровать файл `LostCyberHamster/EditorLogs/diagnostic_log.txt`.
- Правило: перед ручным чтением diagnostic log в Unity-задачах сверять фактический путь из `DebugManager`/скрипта, а не предполагать расположение по имени папки.

### Не ослаблять общий planner до strategy trace

- Контекст: отладка roof-to-roof planning, где branch не доходил до leaf из-за strategy/runtime reject.
- Неудачный ход: попытаться разрешить частичную ветку в `PlanningGraphBuilder`, не доказав точный локальный gate.
- Рабочий ход: проследить цепочку `DecisionPointDetector -> strategy finder -> runtime resolver`, сравнить ordinary/super outcomes и исправить strategy/runtime семантику.
- Правило: если общий planner отбрасывает branch, сначала доказать ближайший strategy/runtime reject полным trace-ом. Инварианты graph builder ослаблять только после доказательства, что сам invariant неверен, а не потому что leaf не строится.