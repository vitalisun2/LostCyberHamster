# 02. Checkpoint call sites

## Цель

Перевести gameplay boundaries на один commit после полного успеха.

## Depends on

- [01. PlayerProgressCommitter](01-player-progress-committer.md)

## Feature

- [Атомарный локальный commit](../features/01-atomic-local-commit.md)

## Scope

- `MenuEntered`, `SkinPurchased`, `SkinApplied`, `QuestListRefreshed`, `DailyQuestCompleted`, `StorylineQuestCompleted`, `QuestRewardClaimed`, `LevelCompleted`, `CurrentLevelChanged`, `TutorialCompleted`, `AccountPromptStateChanged`, `AppBackgrounded`.
- Удаление прямых gameplay `GameDataManager.SaveData`.
- Отсутствие saves на collect, промежуточном quest progress и UI/read paths.

## Acceptance

- Каждый boundary делает максимум один commit после полного изменения данных.
- Purchase commit выполняется после списания и выдачи.
- Level commit выполняется после stars, unlocks и current level.
- Background и menu checkpoints сохраняют актуальный snapshot.

## Validation

- Unit/integration tests по затронутым use cases.
- [Automated testing plan](../testing.md).

## Out of scope

- Storyline serialization и `StorylineQuestCompleted`.
