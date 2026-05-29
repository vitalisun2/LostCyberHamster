# Jump on from roof: статус на 2026-05-29

## Что сделано

- Добавлена first-class opportunity для сценария `SwitchLane -> JumpOnRoof -> JumpOnFromRoof`.
- Для второго паттерна `test_jump_on_from_roof` бот теперь заранее перестраивается на верхнюю линию, запрыгивает на крышу и напрыгивает с крыши на `smallAlive`.
- Target после крыши ищется через существующую `JumpOnFromRoofTargetChainBuilder`, поэтому учитывается конец всей passive roof-chain, а не только первая крыша.
- Проверено на `01_New_York/Morning/test_jump_on_from_roof` с `TimeScale=1` и `TimeScale=2`: уровень завершается `WIN`, второй паттерн проходит нужной цепочкой действий.

## Что стоит обдумать дальше

- Подумать над явным слоем objective/opportunity detectors: например, отдельные детекторы для ground jump-on opportunity и roof jump-on opportunity. Это может лучше разделить ответственность между поиском цели и построением state-specific chain.
- Не объединять преждевременно все chain builder'ы в один универсальный builder: сейчас у ground jump-on, jump-on-from-roof и roof jump-on opportunity разные входные состояния и разные доменные правила.

## Что осталось

- В тестовом уровне остаётся ещё один проблемный паттерн по семейству `JumpOnFromRoof`: уровень проходит, но один из более поздних паттернов всё ещё приводит к damage и требует отдельного анализа.
