# 01. PlayerProgressCommitter

## Цель

Создать единый local commit boundary для целого `PlayerData`.

## Depends on

- Нет.

## Feature

- [Атомарный локальный commit](../features/01-atomic-local-commit.md)

## Scope

- `PlayerProgressCommitter.Commit(CheckpointReason reason)`.
- Каталог из 12 согласованных reasons.
- Один local save после полного успешного domain change.
- Запрет save внутри `ResourceManager`.

## Acceptance

- Successful operation меняет данные полностью и вызывает один commit.
- Failed business operation, negative add, overflow и insufficient funds не меняют данные и не commit.
- Commit получает правильный reason ровно один раз.
- `CheckpointReason` не ветвляет поведение.

## Validation

- Unit test проверяет итоговый persistence effect commit.
- Exhaustive call-site review подтверждает один commit и правильный reason на каждом boundary.
- Targeted diff-check прямых gameplay `SaveData` call sites.

## Out of scope

- Gameplay checkpoint migration.
