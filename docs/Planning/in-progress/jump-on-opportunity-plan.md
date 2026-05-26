# Jump On Opportunity Plan

## Цель
Доработать `JumpOn` так, чтобы бот рассматривал `smallAlive` не только как ground danger, но и как planning target для безопасного напрыгивания. Если энергии `>= 40`, безопасный `JumpOn` по target должен иметь приоритет над альтернативами (`SwitchLane`, `JumpOver`, отсутствие действия), кроме негативных сценариев, где полный action приводит к damage.

## Архитектурный подход
`smallAlive` не меняет базовую физическую семантику: при обычном беге это опасность, при валидном `JumpOn` это target. Поэтому доработка должна жить в planning-interest слое, а не в `ObstacleClassifier` как глобальная переклассификация.

Основной образец для реализации - `JumpOnRoof` и `JumpFromRoofOnRoof`: target может находиться внутри chain и отличаться от первого blocker/trigger obstacle. Resolver подтверждает сам outcome, а planner дополнительно проверяет состояние после полного action.

## Шаги реализации

### 1. Добавить planning interest для JumpOn opportunity
**Где:** `DecisionPointDetector`, `DecisionPoint`, при необходимости небольшой shared model/helper в `Planning/DecisionPoints`.

**Что меняется:** detector должен уметь находить не только blocking danger на текущей линии, но и `JumpOn` opportunity на линии, где есть достижимый `smallAlive`.

**Логика:** если на текущей линии нет более раннего обязательного blocker-а, а энергии достаточно для high-priority jump-on сценария, detector строит decision point по opportunity-chain. Это позволит первому паттерну `test_jump_on` не превращаться в `NO_DECISION`.

**Уточнение:** если blocker на текущей линии и off-line chain с `smallAlive` образуют один близкий cross-lane cluster, detector тоже может выбрать `JumpOnOpportunity`; switch перед таким opportunity должен оставаться ограничен ближайшим blocker-ом текущей линии.

**Уточнение 2:** `JumpOnOpportunity` - это optional target в пределах видимого экрана, а не только ближайшая опасность. Planner должен пробовать построить к нему путь через любые доступные actions, но отсутствие достижимого пути не должно блокировать обычный безопасный план.

### 2. Строить chain для target-линии
**Где:** `DecisionPointDetector` / chain builder.

**Что меняется:** chain builder должен уметь строить цепочку по указанной линии, а не только по `planningState.IsOnBottomLine`.

**Логика:** для jump-on opportunity важно видеть группу препятствий до target. В сценарии `smallNotAlive -> smallAlive` target - второй obstacle, а первый obstacle является частью chain, которую прыжок должен покрыть.

### 3. Перевести ground JumpOn на target внутри chain
**Где:** `JumpOnSpecification`, `JumpOnWindowCalculator`, `JumpOnFireWindowFinder`, `JumpOnWindowModel`, `JumpOnRetainedActionValidator`.

**Что меняется:** `JumpOn` больше не должен требовать, чтобы `chain.FirstObstacle` был `smallAlive`. Он должен искать первый валидный `smallAlive` target внутри chain и возвращать его world index / instance id.

**Логика:** это повторяет рабочий паттерн `JumpOnRoof`, где target roof ищется внутри chain. Pre-target obstacles ограничивают fire-window, но не становятся target-ом.

### 4. Уточнить fire-window для chain JumpOn
**Где:** `JumpOnWindowCalculator`, `JumpOnFireWindowFinder`.

**Что меняется:** окно запуска считается относительно target `smallAlive`, но правая граница должна учитывать ранний контакт с самым левым obstacle в chain до target.

**Логика:** бот должен успеть прыгнуть до столкновения с pre-target obstacle и при этом попасть runtime resolver-ом именно в target `smallAlive`, а не перелететь или получить damage.

### 5. Сверить travel с реальной JumpOn-анимацией
**Где:** `JumpOnPolicy`, `SuperJumpOnPolicy`, при необходимости общий helper для clip travel.

**Что меняется:** проверить и выровнять planning travel с runtime action. Для обычного `JumpOn` runtime использует `transform_jump_on`, а текущая policy считает travel по `transform_jump`.

**Логика:** post-action safety нельзя считать по другой траектории. Если `transform_jump_on` включает напрыгивание и отскок до `transform_jump_end`, planning должен использовать именно полный travel этого action.

### 6. Добавить post-action safety gate для JumpOn
**Где:** рядом с `JumpOnFireWindowFinder` или отдельный small helper в `Shared/JumpPlanning/JumpOn`; вызов из `JumpOnStrategy` перед созданием action и из `JumpOnRetainedActionValidator`.

**Что меняется:** после resolver-valid попадания на target нужно смоделировать завершение полного action: target удален, hamster вернулся в `Run`, мир сдвинут на `completionWorldShift`.

**Логика:** candidate валиден только если после полного `JumpOn` хомяк не оказывается в немедленной ground collision и следующий obstacle может быть обработан обычным planning graph. Это должно отсечь negative pattern `should not jump on`.

### 7. Обновить simulator только в рамках существующего перехода
**Где:** `JumpOnSimulator`, `PlanningStateTransition.AdvanceAfterTargetRemoval`.

**Что меняется:** сохранить текущую модель `AdvanceAfterTargetRemoval`, но убедиться, что она работает с target index внутри chain, а не только с первым obstacle.

**Логика:** pre-target obstacles после полного action должны быть уже позади; target удаляется; дальнейшая безопасность проверяется post-action gate и следующими шагами planning graph.

### 8. Добавить приоритет JumpOn при энергии >= 40
**Где:** `PlanEvaluator`, `PlanningBranchMetrics` / metadata `PlannedAction`, если нужен явный флаг fulfilled opportunity.

**Что меняется:** среди валидных веток при энергии `>= 40` ветка, которая выполнила `JumpOn` opportunity, должна выигрывать у веток, которые только избегают препятствие или ничего не делают.

**Логика:** приоритет применяется только после всех safety checks. Нельзя делать принудительный выбор `JumpOn` до resolver и post-action validation, иначе сломается negative scenario.

**Уточнение:** если для того же target доступны обычный `JumpOn` и `SuperJumpOn`, обычный `JumpOn` считается preferred action для `test_jump_on`-objective; super-вариант остается только когда обычного кандидата для этого target нет.

**Уточнение 2:** unresolved-blocking проверка не должна считать невзятый optional `JumpOnOpportunity` обязательной ошибкой ветки; иначе planner будет отбрасывать безопасные fallback-ветки.

**Уточнение 3:** при сравнении веток нужно учитывать не только количество выполненных `JumpOn` objectives, но и первый target, который ветка берет. Ветка, которая берет более раннюю visible opportunity, не должна проигрывать более дешевой ветке, которая пропускает текущий target и прыгает только на следующую цель.

### 9. Защитить retained action
**Где:** `RetainedActionRevalidator`, `JumpOnRetainedActionValidator`.

**Что меняется:** retained `JumpOn` должен переискать тот же target внутри актуальной chain, а не требовать совпадения с `chain.FirstObstacle`.

**Логика:** после перехода на target-inside-chain старое условие "target is first obstacle" станет неверным и будет сбрасывать корректные retained actions.

**Уточнение:** нижняя граница fire-window с planning margin применяется при создании нового кандидата, но retained `JumpOn` не должен пересоздаваться каждый кадр только потому, что сохраненный trigger приблизился к этой границе. Для retained action достаточно проверить, что trigger еще не пропущен, не выходит за правую границу окна, resolver попадает в тот же target и post-action safety остается валидной.

### 10. Добавить диагностику и ручную проверку
**Где:** diagnostic logs в detector/window finder/post-safety; `test_jump_on` level.

**Что меняется:** логировать тип interest, target index/instance id, chain index target-а, fire-window, resolver outcome, post-action safety result и причину reject-а.

**Логика:** на `test_jump_on` нужно подтвердить четыре паттерна: три выбирают безопасный `JumpOn`, negative pattern не выбирает `JumpOn`.

## Ограничения
- Не менять глобально семантику `smallAlive`: это одновременно danger для Run и target для валидного JumpOn.
- Не делать hardcoded правило под `test_jump_on`.
- Не выбирать `JumpOn` только по энергии: энергия `>= 40` задает приоритет, но не отменяет safety.
- Не дублировать roof/jump calculators без необходимости; где возможно, использовать существующие паттерны `JumpOnRoof` и `JumpFromRoofOnRoof`.
