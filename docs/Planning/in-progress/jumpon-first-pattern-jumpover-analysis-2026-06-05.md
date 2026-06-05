# JumpOn first pattern regression analysis

## Scope

- Уровень: `01_New_York/Morning/test_jump_on`.
- Регресс: первый паттерн `test_jump_on_01`, `smallAlive` на bottom lane.
- Ожидание: паттерны 1, 3, 4 выполняют `JumpOn`; паттерн 2 не выполняет `JumpOn`, потому что bounce unsafe.

## Key facts

- `RuntimeBotController` идет через role-based `StrategiesNew`.
- `ActionGeneratorNew` строит ветки по текущей и соседней линии; для соседней линии входом является `SwitchLane entry`.
- Для первого паттерна root-ветка строилась правильно: `SwitchLane entry before smallAlive -> JumpOn smallAlive`.
- `PlanEvaluator` выбирал именно objective-ветку с `JumpOn`; evaluator не был причиной регресса.
- Старый `JumpOnRetainedActionValidator` не пересчитывал generation-window для committed `JumpOn`; он проверял target identity, runtime outcome и post-action safety.
- Новый `JumpOnRetainedValidatorNew` дополнительно пересчитывал `JumpOnWindowCalculatorNew` и сравнивал retained trigger с новым planning-window.

## Root Causes

1. `Hamster.IsShifting` не покрывал кадр сразу после `ToggleLane()`: публичная линия уже менялась, а animator transition ещё мог не начаться. Из-за этого `SwitchLane` мог завершаться слишком рано.
2. После исправления shifting остался финальный дефект: committed `JumpOn` выбивался retained-validation в узком окне. Лог показал: `JumpOn` стоял в голове плана, но за кадр до trigger-gate validator/новый planning-window отбрасывал его, и planner заменял действие на `SwitchLane before smallAlive`.

## Fix

- `ShiftTransformAnimatorController`: после `ToggleLane()` держит pending shift, пока animator не дошёл до target state.
- `JumpOnRetainedValidatorNew`: убран повторный расчет generation-window. Retained `JumpOn` теперь, как старая рабочая версия, проверяет target в chain, runtime outcome и post-action safety.
- `TestLevelLauncher`: non-interactive запуск больше не требует сохранять открытые dirty scenes; уровень принудительно переоткрывается без сохранения.

## Validation

- `dotnet build LostCyberHamster/Assembly-CSharp.csproj --no-restore`: `0 Error(s)`, остаются существующие warnings.
- Автопрогон `test_jump_on` с `TimeScale 1`: `WIN`.
- EXEC после фикса:
  - pattern 1: `SwitchLane entry before smallAlive` -> `JumpOn smallAlive`.
  - pattern 2: `JumpOn` не выполняется.
  - pattern 3: `JumpOn smallAlive`.
  - pattern 4: `JumpOn smallAlive`.
