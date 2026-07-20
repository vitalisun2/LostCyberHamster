# 02. Сериализуемый storyline progress

## Что и зачем

Завершённые storyline quests переживают local round-trip. Текущий `Dictionary<string, bool>` заменяется формой, поддерживаемой Unity `JsonUtility`.

## Алгоритм

1. `PlayerData` хранит сериализуемую коллекцию завершённых storyline quest IDs/entries.
2. Storyline completion flow записывает completion в коллекцию.
3. Flow вызывает один `Commit(CheckpointReason.StorylineQuestCompleted)`.

## Критерии приёмки

- Полный `PlayerData` round-trip сохраняет storyline progress.
- Completion имеет один надёжный writer.
- Повторная загрузка не теряет и не дублирует завершённые quests.
- Completion и commit выполняются как одна domain operation.

## Вне рамок

- Изменение quest design.
- Сохранение каждого progress event.
