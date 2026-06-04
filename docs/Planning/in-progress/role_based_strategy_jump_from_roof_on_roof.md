# Role-based strategy: JumpFromRoofOnRoof

## Цель

Перевести `JumpFromRoofOnRoof` на role-based path: стратегия планирует прыжок с текущей крыши на следующую крышу, когда перед сходом есть road blocker и дальше есть target roof.

## Текущая привязка

- `JumpFromRoofOnRoofSpecification` требует `RoofRun`, roof support, not shifting, energy >= `10`.
- `JumpFromRoofOnRoofFireWindowFinder.TryFindTargetRoof` ищет `lastRoof`, blocker для схода и следующую roof target.
- Blocker должен быть внутри старого chain; landing roof сейчас ищется дальше в projected world и подтверждается resolver'ом по instance id.
- Window calculator пересекает roof-run limit, landing limit и bigAlive padding.
- Simulator оставляет hamster в `RoofRun` на новой roof support.

## Role-based доработка

- Создать `JumpFromRoofOnRoofStrategyNew` в `StrategiesNew/JumpFromRoofOnRoof`.
- Использовать `DecisionPointNew.Chain` для подтверждения текущего blocker context.
- Следующую `RoofSupport` можно искать дальше по projected world только как landing support для конкретного action, не как target-hunt и не как отдельную ветку выбора.
- Сохранить проверку blocker внутри focus-chain, чтобы стратегия не прыгала на крышу без актуальной причины.

## Не переносить

- Расширение decision chain под target roof.
- Локальный выбор между roof-to-roof и другими roof-exit actions.

## Проверка

- Если перед сходом нет blocker, roof-to-roof action не создается.
- Если blocker есть, но следующая roof не подтверждается resolver'ом, action не создается.
- После симуляции next state остается `RoofRun` с новым roof support id.
