#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.System;
using GameManagement;
using UnityEngine;
using Vues.GameCore;
using Vues.GameCore.Quests;

namespace Assets.Scripts.DevTools.QuestTesting
{
    /// <summary>
    /// Проводит выбранный квест через реальные игровые события и QuestManager.
    /// </summary>
    public sealed class QuestTestRunner
    {
        private bool _isBusy;
        private QuestCategory _selectedCategory = QuestCategory.Daily;
        private string _selectedQuestId;
        private string _beforeState = "—";
        private string _afterState = "Ожидание QuestManager.Init.";
        private string _status =
            "Выберите квест и начните прогон с Generate/Reset.";

        private QuestTestRunner()
        {
        }

        public static QuestTestRunner Shared { get; } = new();

        public event Action Changed;

        public bool IsBusy => _isBusy;

        public QuestCategory SelectedCategory => _selectedCategory;

        public IReadOnlyList<Quest> AvailableQuests =>
            _selectedCategory == QuestCategory.Daily
                ? QuestManager.DailyQuests
                : QuestManager.StoryQuests;

        public int SelectedQuestIndex
        {
            get
            {
                IReadOnlyList<Quest> quests = AvailableQuests;
                for (int index = 0; index < quests.Count; index++)
                {
                    if (quests[index].Id == _selectedQuestId)
                    {
                        return index;
                    }
                }

                return 0;
            }
        }

        public bool IsReady =>
            Application.isPlaying &&
            ActiveQuest != null;

        public string Title
        {
            get
            {
                if (ActiveQuest == null)
                {
                    return "Квест не загружен";
                }

                return FormatTitle(ActiveQuest);
            }
        }

        public string Kind
        {
            get
            {
                if (ActiveQuest == null)
                {
                    return "—";
                }

                return ActiveQuest.Type switch
                {
                    QuestType.ActionCounter =>
                        $"{ActiveQuest.Type} / {ActiveQuest.ActionId}",
                    QuestType.LevelResult =>
                        $"{ActiveQuest.Type} / " +
                        FormatLevelResultCondition(
                            ActiveQuest.Definition) +
                        $"{ActiveQuest.Definition.RequiredStars} звезды",
                    _ => ActiveQuest.Type.ToString()
                };
            }
        }

        public string BeforeState => _beforeState;

        public string AfterState => _afterState;

        public string Status => _status;

        public bool CanGenerateOrReset => IsReady && !_isBusy;

        public bool CanAdvance =>
            IsReady &&
            !_isBusy &&
            ActiveQuest.Type == QuestType.ActionCounter &&
            ActiveQuest.TargetAmount > 1 &&
            ActiveQuest.CurrentProgress == 0 &&
            !ActiveQuest.IsCompleted;

        public bool CanComplete =>
            IsReady &&
            !_isBusy &&
            (ActiveQuest.Type == QuestType.ActionCounter ||
             ActiveQuest.Type == QuestType.LevelResult) &&
            !ActiveQuest.IsCompleted;

        public bool CanClaimReward =>
            IsReady &&
            !_isBusy &&
            ActiveQuest.CanClaimReward;

        private Quest ActiveQuest
        {
            get
            {
                IReadOnlyList<Quest> quests = AvailableQuests;
                if (quests.Count == 0)
                {
                    return null;
                }

                for (int index = 0; index < quests.Count; index++)
                {
                    if (quests[index].Id == _selectedQuestId)
                    {
                        return quests[index];
                    }
                }

                return quests[0];
            }
        }

        /// <summary>
        /// Возвращает локализованные названия квестов выбранной категории.
        /// </summary>
        public string[] GetQuestOptions()
        {
            IReadOnlyList<Quest> quests = AvailableQuests;
            var options = new string[quests.Count];
            for (int index = 0; index < quests.Count; index++)
            {
                options[index] = FormatTitle(quests[index]);
            }

            return options;
        }

        /// <summary>
        /// Выбирает Daily или Story из активных квестов QuestManager.
        /// </summary>
        public void SelectCategory(QuestCategory category)
        {
            if (_isBusy ||
                category == QuestCategory.None ||
                category == _selectedCategory)
            {
                return;
            }

            _selectedCategory = category;
            _selectedQuestId = null;
            ResetSelectionState();
        }

        /// <summary>
        /// Выбирает активный квест по индексу в текущей категории.
        /// </summary>
        public void SelectQuest(int index)
        {
            IReadOnlyList<Quest> quests = AvailableQuests;
            if (_isBusy || index < 0 || index >= quests.Count)
            {
                return;
            }

            string questId = quests[index].Id;
            if (questId == _selectedQuestId)
            {
                return;
            }

            _selectedQuestId = questId;
            ResetSelectionState();
        }

        /// <summary>
        /// Сбрасывает реальное сохранённое состояние выбранного квеста.
        /// </summary>
        public void GenerateOrReset()
        {
            RunAction(
                "Generate/Reset",
                () =>
                {
                    if (!QuestManager.ResetQuestForTesting(ActiveQuest.Id))
                    {
                        throw new InvalidOperationException(
                            "Выбранный квест не найден в QuestManager.");
                    }
                });
        }

        /// <summary>
        /// Публикует реальное действие до частичного прогресса.
        /// </summary>
        public void Advance()
        {
            if (!CanAdvance)
            {
                return;
            }

            int partialProgress = Math.Min(
                Math.Max(1, ActiveQuest.TargetAmount / 2),
                ActiveQuest.TargetAmount - 1);
            RunActionCounterAttempt(
                ActiveQuest,
                "Advance",
                partialProgress);
        }

        /// <summary>
        /// Публикует точный остаток реальных действий до завершения.
        /// </summary>
        public void Complete()
        {
            if (!CanComplete)
            {
                return;
            }

            Quest quest = ActiveQuest;
            if (quest.Type == QuestType.ActionCounter)
            {
                int remainingProgress =
                    quest.TargetAmount - quest.CurrentProgress;
                RunActionCounterAttempt(
                    quest,
                    "Complete",
                    remainingProgress);
                return;
            }

            RunLevelResultAttempt(quest);
        }

        /// <summary>
        /// Выдаёт награду через основной QuestManager.
        /// </summary>
        public void ClaimReward()
        {
            if (!CanClaimReward)
            {
                return;
            }

            RunAction(
                "Claim Reward",
                () =>
                {
                    if (!QuestManager.ClaimReward(ActiveQuest.Id))
                    {
                        throw new InvalidOperationException(
                            "QuestManager отклонил получение награды.");
                    }
                });
        }

        /// <summary>
        /// Очищает отображаемое состояние после остановки Play Mode.
        /// </summary>
        public void HandlePlayModeStopped()
        {
            ResetTransientState(
                "Play Mode остановлен.",
                "QuestManager недоступен.");
        }

        /// <summary>
        /// Готовит страницу к загрузке QuestManager после запуска Play Mode.
        /// </summary>
        public void HandlePlayModeStarted()
        {
            ResetTransientState(
                "Play Mode запущен. Ожидание QuestManager.Init.",
                "Ожидание QuestManager.Init.");
        }

        private void RunActionCounterAttempt(
            Quest quest,
            string actionName,
            int actionCount)
        {
            RunAction(
                actionName,
                () =>
                {
                    int progressBeforeAttempt = quest.CurrentProgress;
                    int levelId = GetValidAttemptLevelId();

                    // Открываем настоящую попытку через игровой event contract.
                    GameEventsManager.LevelStarted(levelId);
                    for (int index = 0; index < actionCount; index++)
                    {
                        PublishConfiguredAction(quest, index);
                    }

                    // До победы attempt buffer не должен менять сохранённый прогресс.
                    if (quest.CurrentProgress != progressBeforeAttempt)
                    {
                        throw new InvalidOperationException(
                            "Прогресс изменился до победы.");
                    }

                    // Закрываем Daily-попытку победой с одной звездой, не завершая Story-квест.
                    GameEventsManager.LevelCompleted(levelId, 1);
                    int expectedProgress = Math.Min(
                        progressBeforeAttempt + actionCount,
                        quest.TargetAmount);
                    if (quest.CurrentProgress != expectedProgress)
                    {
                        throw new InvalidOperationException(
                            $"Ожидался прогресс {expectedProgress}, " +
                            $"получен {quest.CurrentProgress}.");
                    }
                });
        }

        private void RunLevelResultAttempt(Quest quest)
        {
            RunAction(
                "Complete",
                () =>
                {
                    QuestDefinition definition = quest.Definition;
                    int progressBeforeAttempt = quest.CurrentProgress;
                    if (definition.CountUniqueLevels)
                    {
                        CompleteUniqueLevelResultQuest(quest);
                        return;
                    }

                    int levelId = definition.RequiredLevelId == 0
                        ? GetValidAttemptLevelId()
                        : definition.RequiredLevelId;

                    // Публикуем успешный результат через игровой event contract.
                    GameEventsManager.LevelStarted(levelId);
                    if (quest.CurrentProgress != progressBeforeAttempt)
                    {
                        throw new InvalidOperationException(
                            "Прогресс изменился до победы.");
                    }

                    GameEventsManager.LevelCompleted(
                        levelId,
                        definition.RequiredStars);
                    if (!quest.IsCompleted)
                    {
                        throw new InvalidOperationException(
                            "Событие результата уровня не завершило квест.");
                    }
                });
        }

        private static void CompleteUniqueLevelResultQuest(
            Quest quest)
        {
            QuestDefinition definition = quest.Definition;
            if (!LevelCatalogService.Catalog.TryResolveLocationId(
                    definition.RequiredLocationId,
                    out int locationIndex))
            {
                throw new InvalidOperationException(
                    $"Локация {definition.RequiredLocationId} не найдена.");
            }

            List<string> levelAddresses = LevelManager
                .GetLevelsForPartOfDay(
                    locationIndex,
                    definition.RequiredPartOfDayId)
                .ToList();
            if (levelAddresses.Count != definition.TargetAmount)
            {
                throw new InvalidOperationException(
                    $"Каталог содержит {levelAddresses.Count} уровней, " +
                    $"цель квеста — {definition.TargetAmount}.");
            }

            if (LevelController.Instance == null)
            {
                throw new InvalidOperationException(
                    "LevelController не запущен.");
            }

            string originalLevel =
                GameDataManager.PlayerData?.CurrentLevel;
            try
            {
                // Проверяем, что повтор первого уровня не меняет прогресс.
                CompleteLevel(levelAddresses[0], definition.RequiredStars);
                int progressAfterFirstCompletion = quest.CurrentProgress;
                CompleteLevel(levelAddresses[0], definition.RequiredStars);
                if (quest.CurrentProgress != progressAfterFirstCompletion)
                {
                    throw new InvalidOperationException(
                        "Повтор уровня ошибочно увеличил прогресс.");
                }

                // Завершаем остальные разные уровни выбранной части суток.
                for (int index = 1; index < levelAddresses.Count; index++)
                {
                    CompleteLevel(
                        levelAddresses[index],
                        definition.RequiredStars);
                }
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(originalLevel))
                {
                    LevelController.Instance.SetCurrentLevel(originalLevel);
                }
            }

            if (!quest.IsCompleted)
            {
                throw new InvalidOperationException(
                    "Разные уровни не завершили квест.");
            }
        }

        private static void CompleteLevel(
            string levelAddress,
            int stars)
        {
            LevelController.Instance.SetCurrentLevel(levelAddress);
            if (!LevelManager.TryParseLevelNumber(
                    levelAddress,
                    out int levelId))
            {
                throw new InvalidOperationException(
                    $"Уровень {levelAddress} не найден в каталоге.");
            }

            GameEventsManager.LevelStarted(levelId);
            GameEventsManager.LevelCompleted(levelId, stars);
        }

        private void RunAction(string actionName, Action action)
        {
            if (!IsReady || _isBusy)
            {
                return;
            }

            _isBusy = true;
            _beforeState = FormatState();

            try
            {
                action();
                _afterState = FormatState();
                _status =
                    $"{actionName}: {_beforeState} → {_afterState}.";
            }
            catch (Exception exception)
            {
                _status = $"Ошибка {actionName}: {exception.Message}";
                Debug.LogError($"[Quest Testing] {_status}");
            }
            finally
            {
                _isBusy = false;
                Changed?.Invoke();
            }
        }

        private static void PublishConfiguredAction(
            Quest quest,
            int index)
        {
            string sourceId = $"quest-testing-{index + 1}";
            switch (quest.ActionId)
            {
                case GameplayActionIds.ObstacleJumpedOver:
                    GameEventsManager.ObstacleJumpedOver(sourceId);
                    break;
                case GameplayActionIds.ObstacleJumpedOn:
                    GameEventsManager.ObstacleJumpedOn(sourceId);
                    break;
                case GameplayActionIds.ObstacleJumpedOnFromRoof:
                    GameEventsManager.ObstacleJumpedOnFromRoof();
                    break;
                case GameplayActionIds.VehicleRoofRunCompleted:
                    GameEventsManager.VehicleRoofRunCompleted();
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Тест-тул не поддерживает действие {quest.ActionId}.");
            }
        }

        private static int GetValidAttemptLevelId()
        {
            int levelId = LevelManager.GetCurrentLevelNumber();
            if (levelId > 0)
            {
                return levelId;
            }

            foreach (Quest quest in QuestManager.StoryQuests)
            {
                if (quest.Type != QuestType.LevelResult)
                {
                    continue;
                }

                if (quest.Definition.RequiredLevelId > 0)
                {
                    return quest.Definition.RequiredLevelId;
                }

                if (LevelCatalogService.Catalog.TryResolveLocationId(
                        quest.Definition.RequiredLocationId,
                        out int locationIndex))
                {
                    string levelAddress = LevelManager
                        .GetLevelsForPartOfDay(
                            locationIndex,
                            quest.Definition.RequiredPartOfDayId)
                        .FirstOrDefault();
                    if (LevelManager.TryParseLevelNumber(
                            levelAddress,
                            out int catalogLevelId))
                    {
                        return catalogLevelId;
                    }
                }
            }

            throw new InvalidOperationException(
                "Не найден валидный уровень для попытки.");
        }

        private static string FormatTitle(Quest quest)
        {
            string title = LocalizationManager.GetLocalizedString(
                quest.TitleLocalizationKey) ??
                quest.TitleLocalizationKey;
            return $"{title} ({quest.Id})";
        }

        private static string FormatLevelResultCondition(
            QuestDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(
                    definition.RequiredLocationId) &&
                !string.IsNullOrWhiteSpace(
                    definition.RequiredPartOfDayId))
            {
                return $"{definition.RequiredLocationId} / " +
                       $"{definition.RequiredPartOfDayId}, ";
            }

            return definition.RequiredLevelId == 0
                ? "любой уровень, "
                : $"уровень {definition.RequiredLevelId}, ";
        }

        private void ResetSelectionState()
        {
            _beforeState = "—";
            _afterState = IsReady
                ? FormatState()
                : "Ожидание QuestManager.Init.";
            _status = ActiveQuest == null
                ? "В выбранной категории нет активных квестов."
                : $"Выбран {Title}.";
            Changed?.Invoke();
        }

        private void ResetTransientState(
            string status,
            string afterState)
        {
            _isBusy = false;
            _beforeState = "—";
            _afterState = afterState;
            _status = status;
            Changed?.Invoke();
        }

        private string FormatState()
        {
            if (ActiveQuest == null)
            {
                return "QuestManager не инициализирован";
            }

            string progress =
                $"{ActiveQuest.CurrentProgress}/{ActiveQuest.TargetAmount}";
            if (ActiveQuest.IsRewardClaimed)
            {
                return $"Награда получена, {progress}";
            }

            if (ActiveQuest.IsCompleted)
            {
                return $"Выполнен, {progress}";
            }

            if (ActiveQuest.CurrentProgress > 0)
            {
                return $"Частично выполнен, {progress}";
            }

            return $"Сгенерирован, {progress}";
        }
    }
}
#endif
