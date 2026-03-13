# Bot Debug Roadmap

Поэтапный план отладки бота от простого к сложному.

**Статус:** Черновик, требует совместной проработки.

---

## 1. Проблема

Бот реализован по архитектуре (pipeline из 8 стадий), компилируется, запускается, принимает решения — но **не может стабильно пройти уровень без потери жизней**. Текущий подход "внесли правку → проверили на полном уровне" не работает, потому что:

- Одновременно задействованы все 8 стадий pipeline
- При потере жизни невозможно изолировать причину: классификация? проекция? timing? scoring?
- Из сессионных логов видно решения с confidence 0.30 и score -79.5
- Нет способа отличить "бот ошибся в выборе" от "выбор верный, но исполнен не вовремя"

---

## 2. Принципы отладки

### 2.1. Инкрементальное наращивание сложности
Не тестировать всё сразу. Начать с минимального сценария, добиться 100% стабильности, добавить следующий слой.

### 2.2. Изоляция переменных
Одна итерация — одна причина. Не вносить 3-4 изменения одновременно. Чинить один класс проблем за раз.

### 2.3. Три уровня анализа ошибки
При каждом провале определять, на каком уровне проблема:

| Уровень | Вопрос | Пример |
|---------|--------|--------|
| **A. Решение** | Бот правильно понял ситуацию и выбрал верное действие? | Классифицировал BigNotAlive как Threat → выбрал SwitchLane (верно) |
| **B. Планирование** | Цепочка из 2+ шагов корректна? | Jump + SwitchLane, но после Jump приземление в стену (неверно) |
| **C. Исполнение** | Timing правильный? | SwitchLane выбран верно, но исполнен слишком поздно (dist=0.5 вместо 4.0) |

### 2.4. Детерминированность
На каждом этапе: один и тот же тестовый уровень, одинаковый старт, несколько повторов. Цель: **10 из 10 прогонов без потери жизней** на текущем этапе, прежде чем двигаться дальше.

### 2.5. Приоритеты
1. Не терять жизнь (survival)
2. Проходить сценарий стабильно (consistency)
3. Делать это осмысленно (правильные действия)
4. Собирать бонусы (optimization)
5. Максимизировать выгоду (scoring)

---

## 3. BotDebugProfile — модульные переключатели

Static конфиг (или ScriptableObject), управляющий возможностями бота. Не удаление кода — `if`-гарды в pipeline.

```
// --- Доступные действия ---
EnableSwitchLane            = true/false
EnableJump                  = true/false
EnableSuperJump             = true/false
EnableRoofMechanics         = true/false   // вход/выход с крыши
EnableUlta                  = true/false

// --- Цели и приоритеты ---
EnableThreatHandling        = true/false   // избегание угроз
EnableTargetPrioritization  = true/false   // SmallAlive как цель для JumpOn
EnableCollectibles          = true/false   // бонусы при планировании

// --- Планирование ---
MaxChainDepth               = 1..5         // лимит глубины цепочки
EnableKeepTail              = true/false   // частичный replan vs всегда FullRebuild
```

**Точки интеграции:**
- `ChainGenerator` — фильтровать доступные варианты действий по флагам, ограничивать рекурсию по MaxChainDepth
- `ObjectClassifier` — при выключенных Target/Collectibles все объекты этих категорий → Neutral
- `PlanValidator` — при выключенном KeepTail всегда возвращать FullRebuild
- `ChainScorer` — при выключенных Collectibles вес бонусов = 0

**Реализация:** ScriptableObject с Inspector UI, чтобы можно было переключать прямо в Unity Editor во время play mode.

---

## 4. Система логирования

### 4.1. Текущее состояние логов

**Что логируется сейчас:**
- Replan решения (количество кандидатов, лучший score, выбранный план)
- Каждое выполненное действие (тип, state, позиция, live distance)
- SCAN (объекты на обеих полосах с дистанциями)
- Game events (JumpedOn, Damage, Energy, Death)

**Что НЕ логируется (критический пробел):**
- Детали **каждого** сгенерированного кандидата (шаги, score, причина отклонения)
- Решения ObjectClassifier (почему объект получил категорию X)
- Детали collision checks в StateProjector (что конкретно overlap)
- Причины задержки исполнения в BotTimingPolicy (ShouldDelay вернул true — почему?)
- "Вскрытие смерти" — что было в плане в момент получения урона

### 4.2. Расширенное логирование по стадиям pipeline

Формат: `[Stage] Detail`

```
[SNAPSHOT] frame=1234 hamster=(x=-2.96 lane=bottom state=Run energy=85) visibleObjects=6
[CLASSIFY] #1 smallNotAliveRoad dist=4.2 lane=bottom → Threat
[CLASSIFY] #2 collectableCoin dist=7.1 lane=top → Neutral (collectibles disabled)
[CLASSIFY] #3 bigNotAlive dist=9.5 lane=bottom → Threat
[GENERATE] depth=1, evaluating 3 objects, maxCandidates=50
  [VARIANT] obj=#1 action=Jump cost=10 safe=true projected=(x=-2.96 lane=bottom state=Run)
  [VARIANT] obj=#1 action=SwitchLane cost=0 safe=true projected=(x=-2.96 lane=top state=Run)
  [VARIANT] obj=#1 action=SuperJump cost=20 safe=true → PRUNED (SuperJump disabled)
[SCORE] candidate[0]: [Jump#1] score=75.0 (safety=50 + cost=-10 + benefit=35) safe=true
[SCORE] candidate[1]: [SwitchLane#1] score=80.0 (safety=50 + cost=0 + benefit=30) safe=true
[SELECT] FullRebuild, chose candidate[1] score=80.0
  plan: [1]SwitchLane(smallNotAliveRoad)@d=4.0
[EXECUTE] step#1 SwitchLane: liveDist=4.02 → FIRE (threshold=4.0)
[RESULT] step#1 completed, hamster now lane=top, lives=3, damage=false ✓
```

### 4.3. Полный дамп всех кандидатов

При каждом replan — лог **всех** сгенерированных цепочек (не только лучшей):

```
[CANDIDATES] replan#10, total=5:
  #0 [Jump→SwitchLane] cost=10 targets=0 safe=true  score=75.0
  #1 [SwitchLane]      cost=0  targets=0 safe=true  score=80.0 ← SELECTED
  #2 [SuperJump]       cost=20 targets=0 safe=true  score=55.0
  #3 [Jump→Jump]       cost=20 targets=0 safe=false score=-∞   ← UNSAFE: overlap at step2 landing
  #4 [SwitchLane→Jump] cost=10 targets=1 safe=true  score=72.0
```

Это позволит агенту (и человеку) видеть **полную картину принятия решения**: какие альтернативы были, почему были отклонены.

### 4.4. Death Autopsy — "вскрытие" при потере жизни

При каждом получении урона или смерти записывать расширенный блок:

```
[DEATH_AUTOPSY] ============================================
  Time: 38.449s
  Killer: bigAlive dist=0.12 lane=bottom
  Hamster: state=Run lane=bottom energy=69 lives=2→1
  Current plan: [SwitchLane(bigAlive)@d=4.0] status=Ready
  Head step: SwitchLane, status=Ready (НЕ НАЧАТ!)
  Last replan: 0.8s ago, decision=FullRebuild
  Last action executed: Jump @37.2s (1.2s ago)
  Diagnosis: Head step SwitchLane should have fired at d=4.0,
             but current dist=0.12 — step was never executed.
             Possible cause: replan happened too late OR
             BotTimingPolicy didn't match trigger condition.
  All candidates at last replan:
    #0 [SwitchLane] score=-73.8 safe=false ← chose least-bad
    #1 [Jump] score=-90.0 safe=false
    #2 [SuperJump] score=-85.0 safe=false
  Conclusion: ALL candidates unsafe — no good option existed.
[DEATH_AUTOPSY] ============================================
```

### 4.5. Уровни детализации логов

Чтобы не утонуть в информации, три уровня:

| Уровень | Что пишется | Когда использовать |
|---------|-------------|-------------------|
| **Minimal** | REPLAN summary + EXECUTE + DEATH_AUTOPSY | Прогон на полном уровне |
| **Normal** | + CANDIDATES dump + CLASSIFY + RESULT | Отладка конкретного этапа |
| **Verbose** | + каждый VARIANT + SNAPSHOT + collision details | Поиск конкретного бага |

Переключается в BotDebugProfile.

---

## 5. Визуальная отладка

### 5.1. Gizmos overlay в SceneView

Рисовать прямо поверх игры с помощью `OnDrawGizmos` / `Handles`:

- **Зелёная линия** — выбранная цепочка (путь бота)
- **Серые линии** — альтернативные безопасные кандидаты
- **Красные линии** — отклонённые unsafe кандидаты
- **Score** числом над каждой цепочкой
- **Точки приземления** — кружки в местах, где бот планирует приземлиться после Jump
- **Зоны опасности** — красные прямоугольники вокруг Threat объектов (их hitbox)

### 5.2. Slow Motion

Горячая клавиша (например F3): `Time.timeScale = 0.1f` — замедленная съёмка. Видеть момент, когда бот принимает решение и когда перестраивает план.

### 5.3. Step-by-step режим (опционально, поздние этапы)

F2 — пауза `Time.timeScale = 0` в момент каждого replan. Показать все candidates в SceneView. Нажать F2 — продолжить до следующего replan.

---

## 6. Unit-тесты pipeline (без запуска Unity)

Поскольку данные бота (BotSceneSnapshot, ProjectedState) изолированы от Unity runtime, можно тестировать логику pipeline офлайн:

```csharp
[Test]
public void SingleThreat_ShouldJump()
{
    var snapshot = new BotSceneSnapshot {
        HamsterLane = 0,
        HamsterState = HamsterState.Run,
        Energy = 100,
        VisibleObjects = new List<ObstacleInfo> {
            new ObstacleInfo { Type = "smallNotAliveRoad", Lane = 0, Distance = 5.0f }
        }
    };

    var classified = ObjectClassifier.Classify(snapshot);
    var candidates = ChainGenerator.Generate(classified, maxDepth: 1);
    var scored = ChainScorer.Score(candidates);
    var best = PlanSelector.SelectBest(scored);

    Assert.AreEqual(BotAction.Jump, best.Steps[0].Action);
    Assert.IsTrue(best.AllStepsSafe);
}

[Test]
public void BigObstacle_ShouldSwitchLane()
{
    var snapshot = new BotSceneSnapshot {
        HamsterLane = 0,
        HamsterState = HamsterState.Run,
        Energy = 100,
        VisibleObjects = new List<ObstacleInfo> {
            new ObstacleInfo { Type = "bigNotAlive", Lane = 0, Distance = 6.0f }
        }
    };

    var best = RunPipeline(snapshot, maxDepth: 1);

    Assert.AreEqual(BotAction.SwitchLane, best.Steps[0].Action);
}

[Test]
public void TwoObstaclesTrap_ShouldPickJump()
{
    // Оба препятствия на обеих полосах — Jump единственный выход
    var snapshot = new BotSceneSnapshot {
        HamsterLane = 0,
        VisibleObjects = new List<ObstacleInfo> {
            new ObstacleInfo { Type = "smallNotAliveRoad", Lane = 0, Distance = 5.0f },
            new ObstacleInfo { Type = "smallNotAliveRoad", Lane = 1, Distance = 5.0f }
        }
    };

    var best = RunPipeline(snapshot, maxDepth: 1);

    Assert.AreEqual(BotAction.Jump, best.Steps[0].Action);
}
```

Такие тесты выполняются за миллисекунды и покрывают десятки ситуаций. Можно прогонять после каждого изменения pipeline.

---

## 7. Этапы тестирования

### Фаза 1: Базовое выживание (1 шаг, только Threat)

**BotDebugProfile:**
```
EnableThreatHandling = true
EnableTargetPrioritization = false
EnableCollectibles = false
EnableSwitchLane = true
EnableJump = true
EnableSuperJump = false
EnableRoofMechanics = false
EnableUlta = false
EnableKeepTail = false    // всегда FullRebuild
MaxChainDepth = 1
LogLevel = Verbose
```

| Тест | Уровень | Ожидание | Критерий |
|------|---------|----------|----------|
| **1.0** | Пустой (нет препятствий) | Pipeline работает, 0 действий, 0 replan | Нет ошибок, нет ложных срабатываний |
| **1.1** | 1 SmallNotAlive на своей полосе | Jump | 0 жизней потеряно, Jump выполнен при dist ~1.5 |
| **1.2** | 1 BigNotAlive на своей полосе | SwitchLane | 0 жизней, SwitchLane при dist ~4.0 |
| **1.3** | 1 SmallNotAlive на ДРУГОЙ полосе | Ничего (не мешает) | 0 действий, проехал мимо |
| **1.4** | 1 SmallNotAliveRoadAndRoof на своей полосе | Jump | 0 жизней |

**Что проверяется:** SnapshotBuilder, ObjectClassifier (категоризация), ChainGenerator (1 шаг), ChainScorer, BotTimingPolicy (базовый timing Jump и SwitchLane).

**Типичные причины провала:** неправильная дистанция срабатывания, ошибка классификации, collision detection ложноположительные/ложноотрицательные.

---

### Фаза 2: Цепочки из 2 шагов

**BotDebugProfile:** как Фаза 1, но `MaxChainDepth = 2`.

| Тест | Уровень | Ожидание | Критерий |
|------|---------|----------|----------|
| **2.1** | 2 SmallNotAlive подряд, одна полоса, далеко друг от друга | Jump → Jump | 0 жизней, 2 Jump подряд |
| **2.2** | 2 SmallNotAlive подряд, одна полоса, близко (gap < JumpTravel) | SwitchLane (заранее) или Jump + SwitchLane | 0 жизней, бот НЕ приземляется в стену |
| **2.3** | SmallNotAlive + BigNotAlive на одной полосе | Jump + SwitchLane | 0 жизней |
| **2.4** | BigNotAlive на одной полосе + SmallNotAlive на другой (стагнация) | SwitchLane + Jump | 0 жизней |
| **2.5** | Оба препятствия на обеих полосах, одно jumpable | Jump (единственный выход) | 0 жизней, Jump как спасение |

**Что проверяется:** рекурсивная генерация цепочек, проекция состояния после первого действия, pruning тупиковых ветвей, корректность landing position.

**Типичные причины провала:** StateProjector даёт неверную позицию после Jump, overlap detection на step2, timing второго действия "наследует" задержку первого.

---

### Фаза 3: Углублённое планирование (3+ шагов)

**BotDebugProfile:** `MaxChainDepth = 3`, затем 4, затем 5. Остальное как Фаза 2.

| Тест | Уровень | Ожидание | Критерий |
|------|---------|----------|----------|
| **3.1** | 3 SmallNotAlive подряд, одна полоса | Серия Jump/SwitchLane | 0 жизней |
| **3.2** | Чередование препятствий на обеих полосах (змейка) | Серия SwitchLane | 0 жизней, без осцилляции |
| **3.3** | Плотный кластер (4-5 препятствий, gap < 2 units) | Оптимальная цепочка | 0 жизней, не застрял |

**Что проверяется:** глубокая рекурсия без взрыва кандидатов (лимит 50), корректность проекции на 3+ шагов вперёд, отсутствие осцилляции планов.

---

### Фаза 4: Roof-механика

**BotDebugProfile:** включить `EnableRoofMechanics = true`, `MaxChainDepth = 3`.

| Тест | Уровень | Ожидание | Критерий |
|------|---------|----------|----------|
| **4.1** | 1 BigNotAlive (машина), пустая крыша | Jump на крышу, проехать, безопасный спуск | 0 жизней |
| **4.2** | BigNotAlive + SmallNotAliveRoadAndRoof на крыше | Jump на крышу → Jump от roof-threat | 0 жизней |
| **4.3** | BigNotAlive, после крыши сразу obstacle на земле | Крыша → проекция спуска → безопасная зона | 0 жизней |
| **4.4** | 2 BigNotAlive рядом (авто-переход между крышами) | Проехать обе крыши | 0 жизней |

**Что проверяется:** roof auto-transition в StateProjector, RunFromRoofVulnerability зона (~1.9 units), timing спуска с крыши, collision detection во время спуска.

**Типичные причины провала:** проекция descent animation (предполагает мгновенный спуск, реально ~0.5s), lane tracking при спуске.

---

### Фаза 5: Targets (JumpOn для награды)

**BotDebugProfile:** включить `EnableTargetPrioritization = true`.

| Тест | Уровень | Ожидание | Критерий |
|------|---------|----------|----------|
| **5.1** | 1 SmallAlive на своей полосе, ничего после | JumpOn (не JumpOver) | Получен бонус, bounce safe |
| **5.2** | SmallAlive + obstacle после bounce zone | JumpOn только если bounce безопасен | 0 жизней |
| **5.3** | SmallAlive на чужой полосе | SwitchLane + JumpOn (если выгодно) или пропустить | 0 жизней |
| **5.4** | BigAlive (атака с крыши) | RoofJump если на крыше | 0 жизней |

**Что проверяется:** приоритизация Target над простым Avoid, проекция bounce после JumpOn (3.5 units), ChainScorer правильно оценивает выгоду vs риск.

---

### Фаза 6: Collectibles

**BotDebugProfile:** включить `EnableCollectibles = true`.

| Тест | Уровень | Ожидание | Критерий |
|------|---------|----------|----------|
| **6.1** | Монета на безопасной полосе | SwitchLane для сбора (если бесплатно) | Собрана |
| **6.2** | Монета на опасной полосе (за threat) | Игнорировать | 0 жизней > монета |
| **6.3** | Energy pickup на пути | Собрать без специальных действий | Собрана |
| **6.4** | Микс: threat + collectible рядом | Survival > collection | 0 жизней |

**Что проверяется:** ChainScorer корректно оценивает бонусы, survival ВСЕГДА выше любого бонуса.

---

### Фаза 7: KeepTail и оптимизации

**BotDebugProfile:** включить `EnableKeepTail = true`, полная конфигурация.

| Тест | Уровень | Ожидание | Критерий |
|------|---------|----------|----------|
| **7.1** | 5+ препятствий, микс типов | Частичный replan без осцилляции | 0 жизней, replan count разумный |
| **7.2** | Длинный паттерн уровня (15+ объектов) | Стабильное прохождение | 0 жизней |
| **7.3** | Полный реальный уровень | Финал | 0 жизней, 10/10 прогонов |

---

### Фаза 8: SuperJump и Ulta

**BotDebugProfile:** включить остальное (`EnableSuperJump`, `EnableUlta`).

| Тест | Уровень | Ожидание | Критерий |
|------|---------|----------|----------|
| **8.1** | Ситуация где SuperJump единственный выход | SuperJump | 0 жизней |
| **8.2** | 3+ threats рядом (ulta scenario) | UseUlta если заряжена | 0 жизней |

---

## 8. Правила перехода между фазами

1. **Не переходить к следующей фазе**, пока текущая не пройдена 10/10 (10 прогонов подряд, 0 потерянных жизней).
2. При провале — определить уровень проблемы (Решение / Планирование / Исполнение), зафиксировать в DEATH_AUTOPSY.
3. Чинить **один класс проблем** за итерацию. Не 3-4 изменения одновременно.
4. После исправления — повторить все тесты текущей фазы (регрессия).
5. Unit-тесты для pipeline добавлять параллельно — каждый найденный баг → новый тест.

---

## 9. Формат трекинга прогресса

Для каждой фазы вести лог:

```
## Фаза 1: Базовое выживание

### Тест 1.1: Single SmallNotAlive Jump
- [x] Прогон 1: PASS (Jump @dist=1.48, 0 жизней)
- [x] Прогон 2: PASS
- ...
- [x] Прогон 10: PASS
- Статус: ✅ PASSED

### Тест 1.2: Single BigNotAlive SwitchLane
- [x] Прогон 1: FAIL — SwitchLane fired at dist=0.8, too late
  - Death Autopsy: BotTimingPolicy threshold=4.0 but liveDist was never checked (bug in Update loop)
  - Fix: [описание фикса]
- [x] Прогон 2 (after fix): PASS
- ...
```

---

## 10. Порядок имплементации

### Шаг 1: BotDebugProfile
- Создать ScriptableObject с toggles
- Интегрировать `if`-гарды в ChainGenerator, ObjectClassifier, PlanValidator, ChainScorer
- Inspector UI для переключения в play mode

### Шаг 2: Расширенное логирование
- Pipeline-stage логирование (`[SNAPSHOT]`, `[CLASSIFY]`, `[GENERATE]`, `[SCORE]`, `[SELECT]`, `[EXECUTE]`, `[RESULT]`)
- Полный дамп всех кандидатов при каждом replan
- Death Autopsy при каждой потере жизни
- Уровни детализации (Minimal / Normal / Verbose)

### Шаг 3: Тестовые уровни
- Подготовить минимальные уровни для Фазы 1 (4-5 микро-сценариев)
- Каждый уровень = 1-2 препятствия определённого типа на определённых позициях

### Шаг 4: Фаза 1 проход
- Запуск, анализ логов, фиксы
- 10/10 на всех тестах Фазы 1

### Шаг 5: Unit-тесты (параллельно)
- Тесты pipeline без Unity: fake snapshots → проверка решений
- Покрытие ключевых сценариев каждой фазы

### Шаг 6: Визуальная отладка (по необходимости, начиная с Фазы 3-4)
- Gizmos overlay: зелёная (выбранная) / серые (альтернативы) / красные (unsafe) цепочки
- Точки приземления, зоны опасности
- Score над каждой цепочкой
- Slow motion (F3) и step-by-step (F2)

### Шаг 7: Фазы 2-8
- Последовательно, с нарастающей сложностью

---

## 11. Известные подозрительные области (из анализа кода и логов)

Конкретные зоны кода, которые стоит проверить в первую очередь:

| Зона | Подозрение | Как проверить |
|------|-----------|---------------|
| **BotTimingPolicy: SwitchLane completion** | `IsShifting` может не сбрасываться → step никогда не завершается | Verbose лог: время между FIRE и COMPLETED |
| **StateProjector: landing position после Jump** | Может не учитывать ширину хомяка | Unit-тест: Jump + проверка overlap на landing |
| **ChainGenerator: FindNextActionableObject** | Берёт БЛИЖАЙШИЙ, но может пропустить кластер | Verbose лог: какие объекты игнорируются и почему |
| **StateProjector: roof auto-descent timing** | Предполагает мгновенный спуск, реально ~0.5s анимации | Тест 4.3: obstacle сразу после крыши |
| **ChainScorer: unsafe candidate scoring** | Все кандидаты unsafe → выбирает "наименее плохой" с score < 0 | Death Autopsy: были ли safe кандидаты вообще |
| **BotTimingPolicy: ShouldDelayJumpOver** | worldShift cache статичен, не учитывает динамику | Verbose лог: cache values vs actual travel distance |
| **SwitchLane oscillation** | Skip frame после action может быть недостаточен | Session log: повторяющиеся SwitchLane с одинаковым score |

---

## 12. Открытые вопросы для обсуждения

1. **Формат тестовых уровней:** отдельные сцены / JSON-конфиги / встроенный генератор? Как проще всего подготовить микро-сценарии.

2. **Автоматический прогон 10/10:** стоит ли сделать скрипт, который запускает бота N раз и считает winrate? Или отслеживать руками.

3. **Визуальная отладка:** начинать сразу с Gizmos или оставить на поздние фазы? (Мой совет: логов достаточно до Фазы 3, потом добавить Gizmos.)

4. **Unit-тесты:** текущие data structures (BotSceneSnapshot и т.д.) достаточно изолированы от Unity, или нужен рефакторинг для тестируемости?

5. **GodMode vs Survival:** на ранних фазах лучше тестировать в GodMode (бессмертие, но логировать "виртуальные" потери) или в Survival (реальные последствия)?

6. **Тестовый уровень:** нужно ли будет добавить способ быстро задавать препятствия (мини-editor), или хватит ручной расстановки в существующем level editor?
