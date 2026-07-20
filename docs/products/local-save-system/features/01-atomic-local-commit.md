# 01. Атомарный локальный commit

## Что и зачем

Каждая успешная domain operation сохраняет один целостный `PlayerData` после завершения всех изменений. Частичное состояние и повторные saves одной операции исключены.

## Алгоритм

1. Use case полностью меняет `PlayerData`.
2. Use case вызывает `PlayerProgressCommitter.Commit(CheckpointReason reason)`.
3. Committer один раз сохраняет локальный snapshot.

## Checkpoint reasons

`MenuEntered`, `SkinPurchased`, `SkinApplied`, `QuestListRefreshed`, `DailyQuestCompleted`, `StorylineQuestCompleted`, `QuestRewardClaimed`, `LevelCompleted`, `CurrentLevelChanged`, `TutorialCompleted`, `AccountPromptStateChanged`, `AppBackgrounded`.

## Критерии приёмки

- Покупка, quest completion/reward, применение скина, результат уровня, tutorial completion и background сохраняются после полного успеха.
- Menu entry остаётся страховочным checkpoint.
- Failed operation не сохраняет частичное состояние.
- Одна operation создаёт не больше одного save.
- `ResourceManager`, collect events, промежуточный quest progress и UI/read paths не сохраняют.
- `CheckpointReason` не меняет поведение commit.

## Вне рамок

- Load, recovery и reset.
