# Role-based strategy: JumpOnRoof

## Цель

Перевести `JumpOnRoof` на role-based chain: стратегия добавляет обычное запрыгивание на крышу, если в текущей focus-ситуации есть `RoofSupport` и посадка безопасно подтверждается resolver'ом.

## Текущая привязка

- `JumpOnRoofSpecification` требует: hamster не на крыше, не shifting, energy >= `10`.
- `JumpOnRoofFireWindowFinder` ищет первую крышу в старом chain и проверяет landing window.
- Runtime outcome должен вернуть ожидаемый roof state и target roof index.
- `JumpOnRoofSimulator` переводит planning state в `RoofRun`.

## Role-based доработка

- Создать `JumpOnRoofStrategyNew` в `StrategiesNew/JumpOnRoof`.
- Искать первый `RoofSupport` в `DecisionPointNew.Chain`.
- Сохранить window/resolver validation и симуляцию посадки на roof support.
- Не создавать отдельный roof-target builder: роль `RoofSupport` уже факт obstacle.

## Не переносить

- `RoofJumpOnTargetChainBuilder`.
- Optional decision point для roof-jump-on.
- Локальное решение, выгодна ли крыша: это делает evaluator по ветке.

## Проверка

- `bigNotAlive` и `mediumNotAlive` могут породить `JumpOnRoof`, если landing window safe.
- После симуляции следующий decision point строится как roof-run state.
- Если посадка близко к occupant/hazard небезопасна, action не добавляется.
