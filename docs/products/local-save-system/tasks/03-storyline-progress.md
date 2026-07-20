# 03. Storyline progress

## Цель

Сделать storyline completion сериализуемым и долговечным.

## Depends on

- [01. PlayerProgressCommitter](01-player-progress-committer.md)

## Feature

- [Сериализуемый storyline progress](../features/02-serializable-storyline-progress.md)

## Scope

- Замена `Dictionary<string, bool>` на сериализуемую коллекцию IDs/entries.
- Writer в storyline completion flow.
- Один `Commit(CheckpointReason.StorylineQuestCompleted)` после записи completion.

## Acceptance

- Full `PlayerData` round-trip сохраняет completion.
- Повторный load не создаёт duplicate completion.
- Completion flow не сохраняет промежуточный progress.

## Validation

- Narrow integration round-trip test.
- [Automated testing plan](../testing.md).

## Out of scope

- Quest design.
