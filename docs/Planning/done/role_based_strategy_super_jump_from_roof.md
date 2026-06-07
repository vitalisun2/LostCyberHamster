# Role-based strategy: SuperJumpFromRoof

## Цель

Перевести `SuperJumpFromRoof` на role-based path как super-вариант прыжка с крыши на дорогу перед blocking threat.

## Текущая привязка

- `SuperJumpFromRoofStrategy` использует shared `JumpFromRoof` flow.
- `SuperJumpFromRoofPolicy` задает `BotActionKind.SuperJumpFromRoof`, energy cost `20`, super clips и upgrade travel.
- Applicability такая же: roof-run state, опасный автоматический сход и runtime-valid fire window.

## Role-based доработка

- Создать `SuperJumpFromRoofStrategyNew` в `StrategiesNew/SuperJumpFromRoof`.
- Читать `BlockingThreat` из `DecisionPointNew.Chain`.
- Сохранять исключение `RoofOccupantHazard` и проверку gap < `RunFromRoofTravel`.
- Добавлять super action независимо от ordinary candidate.

## Не переносить

- Старый `DecisionPoint` contract.
- Дедупликацию с `JumpFromRoof`.

## Проверка

- Если ordinary и super оба safe, оба попадают в graph.
- Если value одинаковый, evaluator выберет ordinary из-за energy `10` против `20`.
- Если super единственный safe вариант, его branch остается.
