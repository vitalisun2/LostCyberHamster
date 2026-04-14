# Полноценный graph planner для нового runtime-бота

## Цель

Перестроить planner в `Assets/Scripts/Bot/` из линейного greedy-прохода в полноценный state graph search:

- из одного состояния строить все доступные действия;
- каждое действие порождать как отдельную ветвь состояния;
- разворачивать дерево/граф до ограниченной глубины;
- сравнивать целые цепочки действий, а не только локальный лучший шаг;
- сохранить текущую стратегию только `SwitchLaneStrategy`, но сделать архитектуру готовой к добавлению новых стратегий.

## Архитектурные принципы

- Не использовать код старого бота как основу реализации.
- Не смешивать runtime execution и planner graph search.
- Планировщик должен оставаться чистым слоем без Unity lifecycle.
- Стратегии должны генерировать действия для узла графа, не зная про глобальный поиск.
- Симуляция перехода должна быть единой точкой изменения planning-state.

## Целевой дизайн

### 1. PlanningState

Остаётся минимальным снимком projected runtime-state для planner-а. Используется как payload узла графа.

### 2. Graph node / branch model

Добавить отдельные структуры:

- `PlanningGraphNode` — узел поиска: текущее состояние, depth, шаг от родителя, parent, накопленные метрики.
- `PlanningBranch` — готовая цепочка действий, извлечённая из leaf-узла.
- `PlanningMetrics` — агрегаты ветки: energy cost, depth, first trigger, safety flags.

### 3. Action generation

`ActionGenerator` на вход получает `PlanningState` и snapshot, на выходе возвращает все действия для ближайшей релевантной угрозы. Это остаётся контрактом текущего этапа.

### 4. Graph expansion

Новый `PlanningGraphBuilder`:

- стартует из root state;
- для каждого узла запрашивает все candidate actions;
- симулирует переход для каждого действия;
- создаёт дочерние узлы;
- завершает ветвь по одному из условий:
  - кандидатов больше нет;
  - достигнут лимит глубины;
  - переход невалиден;
  - найден цикл по obstacle/line signature.

### 5. Branch evaluation

Новый evaluator сравнивает именно ветви целиком. На текущем этапе целевая эвристика:

- prefer valid/safe branches;
- затем prefer меньшую суммарную стоимость;
- затем prefer более глубокую решающую цепочку;
- затем prefer более поздний first trigger при равной безопасности и стоимости.

### 6. PlanBuilder

`PlanBuilder` больше не делает greedy-loop. Он:

- строит graph branches;
- выбирает лучшую ветвь через evaluator;
- преобразует её в `BotPlan`.

## Файлы изменений

- `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanBuilder.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanEvaluator.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Planning/ActionGenerator.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanningState.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Planning/TransitionSimulator.cs`
- новые файлы в `LostCyberHamster/Assets/Scripts/Bot/Planning/`
- `LostCyberHamster/Assembly-CSharp.csproj`
- новые EditMode tests для planner graph search

## Валидация

- `recompile_scripts`
- релевантные EditMode tests для planner
- при необходимости `regenerate_project_files`, если добавятся новые `.cs`