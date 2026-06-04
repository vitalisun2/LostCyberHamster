# Role-based strategy: SuperJumpFromRoofOnRoof

## Цель

Перевести `SuperJumpFromRoofOnRoof` как super-вариант roof-to-roof прыжка.

## Текущая привязка

- `SuperJumpFromRoofOnRoofStrategy` использует shared `JumpFromRoofOnRoof` flow.
- `SuperJumpFromRoofOnRoofPolicy` задает `BotActionKind.SuperJumpFromRoofOnRoof`, energy cost `20`, super clips и upgrade travel.
- Target roof подтверждается runtime resolver'ом по instance id.

## Role-based доработка

- Создать `SuperJumpFromRoofOnRoofStrategyNew` в `StrategiesNew/SuperJumpFromRoofOnRoof`.
- Использовать `DecisionPointNew.Chain` только как blocker context.
- Поиск следующей `RoofSupport` дальше в projected world оставить внутри strategy/finder только как проверку landing support конкретного action, а не как target-hunt.
- Добавлять super action независимо от ordinary roof-to-roof candidate.

## Не переносить

- Искусственное расширение chain до target roof.
- Фильтр ordinary-vs-super.

## Проверка

- Ordinary и super roof-to-roof candidates могут сосуществовать.
- Evaluator выбирает более дешевый action при равной branch value.
- Если super единственный проходит resolver/landing window, его branch доступна.
