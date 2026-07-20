# Local Save System

## Назначение

Система хранит целостный `PlayerData` локально и восстанавливает его после restart.

## Инварианты

- Domain operation сначала полностью меняет `PlayerData`, затем делает один commit.
- Gameplay не вызывает `GameDataManager.SaveData` напрямую.
- Частые collect/progress events и read/UI operations не сохраняются.
- Load считает сохранение внешним вводом: migrate, validate, safe repair либо recovery.
- Recovery использует один локальный backup и только затем validated defaults.
- Progress reset не затрагивает `Settings` и account.

## Функции

1. [Атомарный локальный commit](features/01-atomic-local-commit.md)
2. [Сериализуемый storyline progress](features/02-serializable-storyline-progress.md)
3. [Валидация и recovery](features/03-validation-and-recovery.md)
4. [Scoped reset](features/04-scoped-reset.md)

Автоматические проверки: [testing.md](testing.md).

## Границы

- `Settings` остаются отдельным локальным контуром.
- Технические load, reset и recovery не проходят через gameplay commit API.
- Progress reset удаляет primary и backup, не затрагивая Settings и account.
- Field merge облачных и локальных данных не проектируется.
