# LostCyberHamster: runtime hamster-obstacle interaction spec

## 1. Назначение документа

Документ фиксирует runtime-правила взаимодействия хомяка с obstacle во время уровня в состоянии `PLAYING`.

Это точная техническая спецификация для:

- описания поведения runtime;
- верификации того, что справочная документация совпадает с кодом игры;
- проектирования систем, которым нужно повторять или учитывать эти runtime-правила.

Документ описывает только те части gameplay, которые влияют на выбор и исход команд `Tap`, `Jump`, `SuperJump`, `RoofJump` и `SuperRoofJump`.

## 2. Модель runtime

### 2.1. Базовая модель мира

Игра работает как side-scroller с неподвижным по X хомяком и движущимся влево миром.

- позиция хомяка по X: `Consts.HamsterXPos = -3.78`;
- базовая скорость мира: `Consts.GameSpeedBase = 3.8 world units / second`;
- obstacle с walking-анимацией получает дополнительное смещение влево через `ObstacleMoveMechanics`, поэтому его итоговая скорость относительно хомяка равна `4.3 world units / second`;
- obstacle спавнятся паттернами через `ObstacleSpawner`;
- следующий паттерн спавнится только после того, как предыдущий полностью вошел в экран;
- для паттерна `relief` дополнительно требуется таймер `2.0` секунды;
- победа наступает, когда все паттерны уже выданы, список `SpawnedObstacles` пуст и после последнего спавна прошло не менее `2.0` секунд.

### 2.2. Дорожные линии и линии крыш

В runtime есть две дорожные линии:

- верхняя: `Consts.ObstacleY0Pos = -1.8`;
- нижняя: `Consts.ObstacleY1Pos = -2.8`.

Текущая дорожная линия хомяка хранится в `IsOnBottomLine`:

- `false` означает верхнюю линию;
- `true` означает нижнюю линию.

Отдельный уровень движения образуют крыши obstacle типов `bigNotAlive` и `mediumNotAlive`. Их Y-координаты вычисляются как `ObstacleY + BIG_NOTALIVE_HEIGHT_UNITS + RoofOffset`, поэтому в текущем runtime линии крыш равны:

- верхняя крыша: `Consts.ObstacleRoofY0Pos = 0.02`;
- нижняя крыша: `Consts.ObstacleRoofY1Pos = -0.98`.

### 2.3. Геометрия runtime-решений

Все решения принимаются по реальным collider bounds. Для расчета используются:

- левая и правая границы хомяка;
- левая и правая границы obstacle;
- тип obstacle;
- линия obstacle;
- текущее состояние хомяка;
- текущая опорная крыша в `LastObstacle`.

### 2.4. Рабочие термины

Дальше в документе используются следующие термины:

- `same-line obstacle` - obstacle на той же логической линии, что и хомяк;
- `roof obstacle` - obstacle типов `bigNotAlive` и `mediumNotAlive`;
- `non-roof obstacle` - любой obstacle, который не является `roof obstacle`;
- `collectable` - obstacle типов `collectableEnergetic`, `collectablePizza`, `collectableCrystal`, `collectableLife`, `collectableCoin`;
- `roof support` - текущий roof obstacle, на котором стоит хомяк в `RoofRun`, хранится в `LastObstacle`.

## 3. Runtime-объекты

Декоративные объекты в runtime-модели не участвуют. Ниже перечислены только obstacle types, которые влияют на управление хомяком.

| Тип | Угроза | Награда | Крыша | Runtime-роль |
| --- | --- | --- | --- | --- |
| `smallAlive` | да | да | нет | живая цель для прыжка сверху; может давать damage или safe-over в зависимости от jump-типа |
| `bigAlive` | да | да | нет | живая цель без safe-landing в обычном road jump |
| `smallNotAliveRoad` | да | да | нет | дорожное препятствие для `JumpOver` или damage-результата |
| `smallNotAliveRoadAndRoof` | да | да | нет | экземпляр этого типа стоит либо на дороге, либо на крыше; само obstacle крышей не является; в ряде сценариев под ним дополнительно ищется roof obstacle |
| `mediumNotAlive` | да | нет | да | roof platform |
| `bigNotAlive` | да | нет | да | roof platform |
| `collectableEnergetic` | нет | да | нет | при касании добавляет энергию |
| `collectablePizza` | нет | да | нет | при касании добавляет энергию |
| `collectableCrystal` | нет | да | нет | при касании добавляет кристалл |
| `collectableLife` | нет | да | нет | при касании добавляет жизнь |
| `collectableCoin` | нет | да | нет | при касании спавнит coin pickup effect, но по текущему коду отправляет событие `CrystallCollected(1)` |

## 4. Состояние хомяка

### 4.1. Поля runtime-state

Для runtime-взаимодействия с obstacle значимы следующие поля:

- `HamsterState`;
- `IsOnBottomLine`;
- `Lives`, стартовое значение `3`;
- `Energy`, стартовое значение `100`;
- `IsDamaged`;
- `IsShifting`;
- `LastObstacle`;
- `NeedCheckCollisionInRunFromRoofAfterShift`.

### 4.2. Команды и guard-условия

#### `TapRequest`

`TapRequest` обрабатывается `TapMechanics`.

Запрос игнорируется, если одновременно выполняются оба условия:

- `HamsterState` не равен `Run` и не равен `RoofRun`;
- `IsDamaged == false`.

Отдельно `TapRequest` игнорируется, если `IsShifting == true`.

Если запрос принят:

- `ShiftTransformAnimatorController.ToggleLane()` инвертирует animator bool `IsShiftedDown`;
- после этого `IsOnBottomLine` сразу записывается из нового значения `IsShiftedDown()`;
- визуальный переход между линиями идет анимацией, но логическая линия меняется в тот же кадр.

#### `JumpRequest`

`JumpRequest` обрабатывается `JumpMechanics`, если `Energy >= 10`.

Если `IsDamaged == true`, `JumpMechanics.CalculateJumpState()` сразу возвращает нейтральный результат `HamsterStateEnum.Jump` без target.

#### `RoofJumpRequest`

`RoofJumpRequest` обрабатывается `RoofJumpMechanics`, если `Energy >= 10`.

В `RoofJumpMechanics` отдельной проверки `IsDamaged` нет.

#### `SuperJumpRequest`

`SuperJumpRequest` обрабатывается `SuperJumpMechanics`, если после первого прыжка у хомяка осталось не менее `10` энергии.

Поскольку первый `JumpRequest` уже списывает `10`, фактический минимальный запас энергии до старта road super jump равен `20`.

#### `SuperRoofJumpRequest`

`SuperRoofJumpRequest` обрабатывается `SuperRoofJumpMechanics`, если после первого roof jump у хомяка осталось не менее `20` энергии.

Поскольку первый `RoofJumpRequest` уже списывает `10`, фактический минимальный запас энергии до старта roof super jump равен `30`.

### 4.3. Double-tap для super jump

`DoubleJumpDetector` использует окно `DoubleJumpThreshold = 0.3` секунды.

В keyboard flow:

- первое нажатие jump вызывает обычный `OnJump()`;
- второе нажатие jump в пределах `0.3` секунды вызывает `OnSuperJump()`.

`KeyboardMechanics.OnSuperJump()` маршрутизирует второй тап так:

- состояния `Jump`, `JumpOver`, `JumpOnObstacle`, `JumpOnRoof`, `JumpDamageForSmallAlive`, `JumpDamageForSmallNotAlive`, `JumpDamageForBigAlive`, `JumpOnRoofDamage` вызывают `SuperJumpRequest`;
- состояния `RoofJump`, `RoofJumpDamage`, `JumpFromRoof`, `JumpFromRoofDamage`, `JumpOnObstacleFromRoof` вызывают `SuperRoofJumpRequest`.

Этот раздел описывает только runtime keyboard flow. Другие input/execution layers находятся вне области этой спецификации и могут поддерживать подмножество команд.

## 5. Глобальные runtime-правила

### 5.1. Какие obstacles участвуют в расчете прыжка

`CollisionUtils.GetValidObstaclesAhead()` возвращает только obstacles, которые одновременно:

- находятся на той же логической линии, что и хомяк;
- имеют `transform.position.x > hamster.position.x`;
- еще не despawn'нуты и присутствуют в `ObstacleSpawner.Instance.SpawnedObstacles`.

Ранний выход по reach выполняется уже в конкретных mechanics/resolver: проверка прекращается, когда после смещения на длину текущего jump-клипа левая граница следующего obstacle остается правее правой границы хомяка.

### 5.2. Общие правила trigger-collision

`CollisionController` сначала проверяет два общих guard-условия:

- если `IsDamaged == true`, столкновение полностью игнорируется;
- если obstacle не на той же линии, столкновение игнорируется.

После этого логика такая:

- collectable обрабатывается раньше, чем проверка урона;
- damage через trigger работает в состояниях `Run`, `RunFromRoof`, `JumpFromRoof`, `SuperJumpFromRoof`;
- в `RoofRun` урон получает любой non-roof obstacle;
- collectable не наносят урон, потому что обрабатываются и удаляются до damage-ветки.

## 6. Базовые режимы движения

### 6.1. `Run`

В `Run` без input действуют два правила:

- same-line collectable подбирается при касании, если `IsDamaged == false`;
- same-line threat obstacle наносит урон при trigger-contact, если `IsDamaged == false`.

### 6.2. `RoofRun`

В `RoofRun` текущий roof support хранится в `LastObstacle`.

`RoofRunMechanics` каждый кадр выполняет две последовательные проверки:

1. Совпадает ли текущая линия хомяка с линией `LastObstacle`.
2. Ушел ли хомяк правой границей дальше `roofRight + 0.7 * hamsterWidth`.

Если линия больше не совпадает, `HelpMethods.FindBigNotAliveUnderHamster()` ищет новый roof obstacle под хомяком на текущей линии:

- если obstacle найден, `LastObstacle` переключается на него;
- если obstacle не найден, состояние переходит в `RunFromRoof`.

Если хомяк дошел до края текущей крыши, `FindNextBigNotAliveOnSameLine()` ищет следующий roof obstacle впереди на той же линии и проверяет X-overlap с текущими границами хомяка:

- если overlap есть, `LastObstacle` переключается на этот obstacle;
- если overlap нет, состояние переходит в `RunFromRoof`.

В `RoofRun` после collectable-ветки урон наносит любой same-line obstacle, который не является roof obstacle.

### 6.3. `RunFromRoof`

Этот режим возникает, когда хомяк теряет roof support в `RoofRun`.

Для `CollisionController` это одно из состояний, в которых активен обычный trigger damage.

## 7. Прыжки и их исходы

### 7.1. Обычный road jump

`JumpMechanics` использует клип `transform_jump`. Если расчет не нашел специального результата, состояние остается `Jump`.

Исходы по типам obstacle:

- `smallAlive`: центр хомяка внутри obstacle interval в конце клипа дает `JumpOnObstacle`; иначе X-overlap дает `JumpDamageForSmallAlive`; иначе полный перелет по X дает `JumpOver`;
- `smallNotAliveRoad`: X-overlap дает `JumpDamageForSmallNotAlive`; полный перелет по X дает `JumpOver`;
- `smallNotAliveRoadAndRoof`: если obstacle полностью перелетели по X, результат `JumpOver`; если X-overlap отсутствует, результат `noHit`; если X-overlap есть и под obstacle найден roof obstacle, результатом становится `JumpOnRoof` или `JumpOnRoofDamage`; иначе результатом становится `JumpDamageForSmallNotAlive`;
- `bigAlive`: `JumpDamageForBigAlive` возникает, если есть X-overlap в конце клипа или Y-overlap в середине клипа; safe-result для этого типа road jump не дает;
- `bigNotAlive` и `mediumNotAlive`: X-overlap в конце клипа дает `JumpOnRoof` или `JumpOnRoofDamage`.

Пост-обработка по animation events:

- `transform_jump_end` переводит road jump обратно в `Run`;
- если состояние было `JumpOver`, вызывается `JumpOverEvent`;
- если состояние было `JumpDamageForSmallAlive` или `JumpDamageForSmallNotAlive`, damage применяется в `transform_jump_end`;
- если состояние было `JumpDamageForBigAlive`, damage применяется в `transform_jump_mid`;
- `transform_jump_on_roof_end` переводит `JumpOnRoof` и `JumpOnRoofDamage` в `RoofRun`; damage-вариант сначала вызывает `DamageEvent`.

### 7.2. Обычный jump из `RoofRun`

`RoofJumpMechanics` использует два клипа:

- `transform_roof_jump` для прыжка на крышу;
- `transform_jump_from_roof` для спуска или прыжка на живую цель.

Если ни один obstacle handler не вернул специального результата, итогом становится `JumpFromRoof`.

Исходы по типам obstacle:

- `bigNotAlive` и `mediumNotAlive`: X-overlap на длине `transform_roof_jump` дает `RoofJump` или `RoofJumpDamage`;
- `smallAlive` и `bigAlive`: центр хомяка внутри obstacle interval на длине `transform_jump_from_roof` дает `JumpOnObstacleFromRoof`; иначе X-overlap дает `JumpFromRoofDamage`;
- `smallNotAliveRoad`: X-overlap на длине `transform_jump_from_roof` дает `JumpFromRoofDamage`;
- `smallNotAliveRoadAndRoof`: если под obstacle найден roof obstacle, результатом сразу становится `RoofJump` или `RoofJumpDamage`; если roof obstacle под ним нет и есть X-overlap на длине `transform_jump_from_roof`, результатом становится `JumpFromRoofDamage`; иначе special-result нет.

Пост-обработка:

- `transform_roof_jump_end` переводит `RoofJump` и `RoofJumpDamage` в `RoofRun`; damage-вариант сначала вызывает `DamageEvent`;
- `transform_jump_from_roof_end` переводит `JumpFromRoof`, `JumpFromRoofDamage` и `JumpOnObstacleFromRoof` в `Run`; для `JumpFromRoofDamage` damage вызывается перед возвратом в `Run`;
- событие `transform_jumped_on` вызывает уничтожение obstacle и для roof-jump вариантов тоже.

### 7.3. Road super jump

`SuperJumpMechanics` использует клип `transform_super_jump`. Если `IsDamaged == true`, расчет сразу возвращает нейтральный результат `SuperJump`.

Исходы по типам obstacle:

- `bigNotAlive` и `mediumNotAlive`: X-overlap в конце super-jump клипа дает `SuperJumpOnRoof` или `SuperJumpOnRoofDamage`;
- `bigAlive`: X-overlap дает `SuperJumpDamage`; если X-overlap нет, но obstacle полностью перелетели по X, результатом становится `SuperJumpOver`;
- `smallAlive`: центр хомяка внутри obstacle interval дает `SuperJumpOnObstacle`; иначе X-overlap дает `SuperJumpDamage`; иначе полный перелет дает `SuperJumpOver`;
- `smallNotAliveRoad`: X-overlap дает `SuperJumpDamage`; иначе полный перелет дает `SuperJumpOver`;
- `smallNotAliveRoadAndRoof`: если нет ни X-overlap, ни полного перелета, obstacle не влияет на super jump; если под small obstacle найден roof obstacle и есть X-overlap с этим roof obstacle на длине super jump, результатом становится `SuperJumpOnRoof` или `SuperJumpOnRoofDamage`; иначе X-overlap с small obstacle дает `SuperJumpDamage`, а отсутствие overlap при наличии полного перелета дает `SuperJumpOver`.

Пост-обработка:

- `transform_jump_end` переводит `SuperJump`, `SuperJumpOver`, `SuperJumpOnObstacle` и `SuperJumpDamage` в `Run`;
- `SuperJumpOver` вызывает `JumpOverEvent`;
- `SuperJumpDamage` применяет damage в `transform_jump_end`;
- `transform_jump_on_roof_end` переводит `SuperJumpOnRoof` и `SuperJumpOnRoofDamage` в `RoofRun`; damage-вариант сначала вызывает `DamageEvent`.

### 7.4. Roof super jump

`SuperRoofJumpMechanics` использует два клипа:

- `transform_super_roof_jump` для прыжка на крышу;
- `transform_super_jump_from_roof` для спуска или прыжка на цель.

Если obstacle handlers не нашли специального результата, итогом становится `SuperJumpFromRoof`.

Исходы по типам obstacle:

- `bigNotAlive` и `mediumNotAlive`: X-overlap на длине `transform_super_roof_jump` дает `SuperRoofJump` или `SuperRoofJumpDamage`;
- `smallAlive` и `bigAlive`: центр хомяка внутри obstacle interval на длине `transform_super_jump_from_roof` дает `SuperJumpOnObstacleFromRoof`; иначе X-overlap дает `SuperJumpFromRoofDamage`;
- `smallNotAliveRoad`: X-overlap на длине `transform_super_jump_from_roof` дает `SuperJumpFromRoofDamage`;
- `smallNotAliveRoadAndRoof`: если под obstacle найден roof obstacle, `SuperRoofJumpMechanics` сразу возвращает `SuperRoofJump` или `SuperRoofJumpDamage`; если roof obstacle под ним не найден и есть X-overlap на длине `transform_super_jump_from_roof`, результатом становится `SuperJumpFromRoofDamage`; иначе special-result нет.

Пост-обработка:

- `transform_roof_jump_end` переводит `SuperRoofJump` и `SuperRoofJumpDamage` в `RoofRun`; damage-вариант сначала вызывает `DamageEvent`;
- `transform_jump_from_roof_end` переводит `SuperJumpFromRoof`, `SuperJumpFromRoofDamage` и `SuperJumpOnObstacleFromRoof` в `Run`; для `SuperJumpFromRoofDamage` damage вызывается перед возвратом в `Run`;
- `transform_jumped_on` вызывает уничтожение obstacle и для roof-super-jump вариантов.

## 8. Урон, энергия и награды

### 8.1. Жизни и урон

- `Lives` стартует с `3`;
- каждое срабатывание `DamageEvent` уменьшает `Lives` на `1`;
- `TakeDamageMechanics` всегда запускает blink-анимацию и выставляет `IsDamaged = true`;
- `sprite_blink_end` сбрасывает `IsDamaged` обратно в `false`.

### 8.2. Энергия

- максимум энергии: `100`;
- `JumpRequest` списывает `10`, если текущая энергия не меньше `10`;
- `RoofJumpRequest` списывает `10`, если текущая энергия не меньше `10`;
- `SuperJumpRequest` списывает еще `10`, если текущая энергия не меньше `10`;
- `SuperRoofJumpRequest` списывает еще `10`, если текущая энергия не меньше `10`;
- `EnergyMechanics` восстанавливает `1` энергию в секунду, пока энергия меньше `100`.

### 8.3. Награды за прыжок сверху и перелет

`JumpOverEvent` всегда дает `CoinCollected(1)`.

Уничтожение obstacle прыжком сверху вызывает `DestroyObstacleEvent`, после чего `AddCoinsOrBonusMechanics` выбирает награду так:

- с вероятностью `70%` вызывается `CoinCollected(3)`;
- с вероятностью `30%` выбирается бонусная ветка;
- внутри бонусной ветки `85%` дают `+20` энергии, `5%` дают `+1` жизнь с капом `3`, `10%` дают `CrystallCollected(1)`;
- energy bonus визуально спавнит либо energetic effect, либо pizza effect с вероятностью `50/50`; величина награды в обоих случаях одинакова и равна `20`.

### 8.4. Collectable pickup

Collectable подбираются только через `CollisionController`, поэтому во время `IsDamaged == true` они не подбираются.

Эффекты collectable по текущему коду:

- `collectableEnergetic` вызывает `Hamster.AddEnergy()` и добавляет до `30` энергии, но не больше `100`;
- `collectablePizza` вызывает `Hamster.AddEnergy()` и добавляет до `30` энергии, но не больше `100`;
- `collectableLife` добавляет `min(1, 3 - Lives)` жизней;
- `collectableCrystal` вызывает `CrystallCollected(1)`;
- `collectableCoin` тоже вызывает `CrystallCollected(1)` и не вызывает `CoinCollected(1)`.

## 9. Завершение уровня

Уровень завершается двумя способами:

- победа: когда все паттерны уже выданы, список `SpawnedObstacles` пуст и после последнего спавна прошло не менее `2.0` секунд;
- поражение: `DeathMechanics` вызывает `LevelController.Instance.Finish()`, когда `Lives == 0`.

## 10. Кодовые точки опоры

Основные файлы, на которые опирается этот документ:

- `Assets/Scripts/Consts.cs`
- `Assets/Scripts/Common/CollisionUtils.cs`
- `Assets/Scripts/Common/DoubleJumpDetector.cs`
- `Assets/Scripts/Common/HelpMethods.cs`
- `Assets/Scripts/Gameplay/Hamster.cs`
- `Assets/Scripts/Gameplay/Obstacle.cs`
- `Assets/Scripts/Gameplay/CollectCoinsOrBonusAction.cs`
- `Assets/Scripts/GameEngine/Controllers/CollisionController.cs`
- `Assets/Scripts/GameEngine/Controllers/ShiftTransformAnimatorController.cs`
- `Assets/Scripts/GameEngine/Mechanics/TapMechanics.cs`
- `Assets/Scripts/GameEngine/Mechanics/JumpMechanics.cs`
- `Assets/Scripts/GameEngine/Mechanics/RoofRunMechanics.cs`
- `Assets/Scripts/GameEngine/Mechanics/RoofJumpMechanics.cs`
- `Assets/Scripts/GameEngine/Mechanics/SuperJumpMechanics.cs`
- `Assets/Scripts/GameEngine/Mechanics/SuperRoofJumpMechanics.cs`
- `Assets/Scripts/GameEngine/Mechanics/HamsterAnimationEventsMechanics.cs`
- `Assets/Scripts/GameEngine/Mechanics/EnergyMechanics.cs`
- `Assets/Scripts/GameEngine/Mechanics/AddOneCoinMechanics.cs`
- `Assets/Scripts/GameEngine/Mechanics/AddCoinsOrBonusMechanics.cs`
- `Assets/Scripts/GameEngine/Mechanics/TakeDamageMechanics.cs`
- `Assets/Scripts/GameEngine/Mechanics/DeathMechanics.cs`
- `Assets/Scripts/System/ObstacleSpawner.cs`
