# SuperJumpOver bigAlive window analysis — 2026-06-13

## Scope

Регресс в `01_New_York/Morning/level_01`: после `JumpOn smallAlive` бот фиксировал `DEAD_END` перед `bigAlive`, хотя вручную участок проходится через `SuperJumpOver`.

## Источники

- `EditorLogs/diagnostic_log.txt`, канал `BOT`, прогон `01_New_York/Morning/level_01`.
- `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpOver/JumpOverChainCalculator.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpOver/JumpOverFireWindowFinder.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpOver/SuperJumpOverPolicy.cs`
- `LostCyberHamster/Assets/Scripts/GameEngine/Controllers/CollisionController.cs`
- `LostCyberHamster/Assets/Scripts/GameEngine/Mechanics/SuperJumpOutcomeResolver.cs`

## Факты

- `SuperJumpOverPolicy.BigAliveCollisionPaddingRatio` равен `CollisionController.BigAliveJumpDamageOverlapThreshold = 0.3`.
- `JumpOverChainCalculator` считает базовое окно запуска, затем сужает его на `hamster.Width * policy.BigAliveCollisionPaddingRatio`.
- В проблемном узле один и тот же `bigAlive` был одновременно первым и последним obstacle chain.
- Лог диагностики показал базовое окно `before=[0.100,1.037]`, но после padding оно стало `after=[0.592,0.545]`.
- До `JumpOverFireWindowFinder` и runtime resolver дело не доходило: окно схлопывалось в `JumpOverChainCalculator`.

## Корень проблемы

`ApplyBigAliveCollisionPadding` применял `bigAlive`-padding к одиночному `bigAlive` с двух сторон: уменьшал `lastFireShift` и одновременно увеличивал `firstFireShift`. Это трактовало один obstacle как две разные границы chain и удаляло валидный ранний старт `SuperJumpOver`.

## Решение

В `JumpOverChainCalculator` отличать одиночный obstacle по `InstanceId`. Для одного и того же `bigAlive` применять только ограничение позднего старта перед первым obstacle. Ограничение раннего старта для последнего `bigAlive` оставлять только когда последний obstacle chain — другой объект.

## Проверка

- `01_New_York/Morning/level_01` после правки проходит проблемный `bigAlive`: план строит `SuperJumpOver bigAlive`, ранний dead-end ушел.
- Дальше уровень падает позже по отдельной причине экономики/энергии; regression set не запускался в этой итерации.
