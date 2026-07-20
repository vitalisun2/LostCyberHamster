# Рефакторинг локальных сохранений

## Checkpoint policy

- `SaveData` вызывается один раз после полного успешного изменения всей операции. Внутри `ResourceManager` сохранения нет.
- Обязательные checkpoints:
  - успешная покупка скина: списание ресурса и выдача скина завершены;
  - завершение daily quest: progress достиг target и выставлен `IsCompleted`;
  - завершение storyline quest: completion записан в сериализуемый storyline progress;
  - получение награды за quest;
  - применение скина: обновлён `AppliedSkinId`;
  - результат уровня: обновлены stars, unlocks и current level;
  - завершение tutorial;
  - уход приложения в background.
- Открытие menu — дополнительный страховочный checkpoint.
- Не сохранять каждую монету, UI/read-операции и промежуточные шаги одной операции.
- Не сохранять каждый quest progress event.
- Не вызывать несколько `SaveData` внутри одной операции.

## PlayerProgressCommitter

- Подход: Unit of Work с единым commit boundary.
- Domain use case полностью меняет `PlayerData`, затем вызывает `PlayerProgressCommitter.Commit(CheckpointReason reason)`.
- Committer выполняет один local save. Позже он передаст тот же snapshot в Cloud Sync queue/retry.
- `CheckpointReason` пока не меняет поведение. Он задаёт каталог checkpoints, упрощает поиск call sites, чтение кода и логи.
- Reasons: `MenuEntered`, `SkinPurchased`, `SkinApplied`, `QuestListRefreshed`, `DailyQuestCompleted`, `StorylineQuestCompleted`, `QuestRewardClaimed`, `LevelCompleted`, `CurrentLevelChanged`, `TutorialCompleted`, `AccountPromptStateChanged`, `AppBackgrounded`.
- Прямые `GameDataManager.SaveData` из gameplay запрещены. Технические load/reset/recovery остаются отдельными.

## Storyline progress

- Проблема: `PlayerData.ComplitedStorylineQuests` использует `Dictionary<string, bool>`. Unity `JsonUtility` не сериализует dictionary; надёжного writer при completion нет, состояние теряется.
- Решение: заменить dictionary на сериализуемую коллекцию завершённых storyline quest IDs/entries.
- Storyline completion flow обновляет коллекцию, затем один раз вызывает `Commit(CheckpointReason.StorylineQuestCompleted)`.

## Валидация игровых данных

- Code contracts предотвращают новые invalid states. Сохранение считается внешним вводом.
- Load pipeline: deserialize, migrate, validate, safe repair, повторная validate.
- Safe deterministic repairs отделены от ambiguous/rejected data.
- Rejected data восстанавливается из backup/default и не перезаписывает good save.
- Programmer errors используют exceptions/assert. Ожидаемые business rejects возвращают обычный result.
- Load exceptions ловятся на persistence boundary.
- `bool ValidateAndRepair` недостаточен: он не различает valid, repaired и rejected.
- API: `PlayerDataValidationResult Validate(PlayerData data)` и `void RepairSafe(PlayerData data, PlayerDataValidationResult result)`.

## Safe reset

- Проблема: `PlayerPrefs.DeleteAll` удаляет progress, `Settings`, feature/tutorial и чужие keys.
- `GameDataManager.ResetPlayerProgress()` удаляет только `PlayerData` key, создаёт default `PlayerData`, выполняет `Validate`, при repairable — `RepairSafe` и повторный `Validate`. Rejected data не сохраняется; после успешной validation сохраняется новый progress.
- `Settings` и account не затрагиваются.
- `ResetSettings()` сбрасывает только настройки.
- Полный DevTools reset только оркестрирует нужные reset operations.
- `PlayerPrefs.DeleteAll` больше не используется.

## Тесты игровых данных

- Unit:
  - valid data остаётся без изменений;
  - safe repair: null collections, точные set-like duplicates, отсутствующий пустой catalog progress; повторная validate успешна;
  - repair идемпотентен;
  - null `PlayerData`, negative/overflow resources, contradictory reward flags, conflicting duplicate level records и unknown purchased skin отклоняются без silent mutation;
  - insufficient funds, negative add и overflow не меняют данные и не вызывают commit;
  - успешная domain operation полностью меняет данные, затем вызывает один commit;
  - checkpoint передаёт правильный reason ровно один раз.
- Обязательные узкие integration tests:
  - missing/corrupt/old save восстанавливается без crash;
  - полный `PlayerData` round-trip сохраняет storyline progress;
  - progress-only reset сохраняет `Settings` и Account.
