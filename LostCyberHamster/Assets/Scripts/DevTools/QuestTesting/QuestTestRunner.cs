#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.System;
using GameManagement;
using GameManagement.Progress;
using UnityEngine;
using Vues.GameCore;
using Vues.GameCore.Quests;

namespace Assets.Scripts.DevTools.QuestTesting
{
    /// <summary>
    /// Проводит выбранный квест через игровые события и QuestManager, не изменяя другие квесты.
    /// </summary>
    public sealed class QuestTestRunner
    {
        private bool _isBusy;
        private QuestCategory _selectedCategory = QuestCategory.Daily;
        private string _selectedQuestId;
        private string _beforeState = "—";
        private string _afterState = "Ожидание QuestManager.Init.";
        private string _status =
            "Выберите квест и доступную команду прогона.";
        private readonly PlayerExperienceService _playerExperienceService =
            new();

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

                string kind = ActiveQuest.Type switch
                {
                    QuestType.ActionCounter =>
                        $"{ActiveQuest.Type} / {ActiveQuest.ActionId}",
                    QuestType.LevelResult =>
                        $"{ActiveQuest.Type} / " +
                        FormatLevelResultCondition(
                            ActiveQuest.Definition) +
                        $"{ActiveQuest.Definition.RequiredStars} звезды",
                    QuestType.PlayerState =>
                        $"{ActiveQuest.Type} / " +
                        $"{ActiveQuest.Definition.StateId} / " +
                        $"{ActiveQuest.Definition.EntityId} >= " +
                        ActiveQuest.Definition.RequiredValue,
                    _ => ActiveQuest.Type.ToString()
                };

                return ActiveQuest.Category == QuestCategory.Daily
                    ? $"{ActiveQuest.Definition.DailyDifficulty} / " +
                      $"{ActiveQuest.Definition.DailyMechanicId} / {kind}"
                    : kind;
            }
        }

        public string BeforeState => _beforeState;

        public string AfterState => _afterState;

        public string Status => _status;

        /// <summary>
        /// Проверяет возможность безопасно сбросить квест и его внешнюю цель.
        /// </summary>
        public bool CanResetQuest =>
            IsReady &&
            !_isBusy &&
            (ActiveQuest.Type != QuestType.PlayerState ||
             CanResetPlayerStateQuest(ActiveQuest));

        public bool CanAdvanceQuestDay =>
            IsReady &&
            !_isBusy;

        public bool CanAdvance =>
            IsReady &&
            !_isBusy &&
            (ActiveQuest.Type == QuestType.ActionCounter ||
             ActiveQuest.Type == QuestType.LevelResult &&
             !ActiveQuest.Definition.CountUniqueLevels) &&
            ActiveQuest.TargetAmount > 1 &&
            ActiveQuest.CurrentProgress == 0 &&
            !ActiveQuest.IsCompleted;

        /// <summary>
        /// Проверяет поддержку штатного пути завершения выбранного квеста.
        /// </summary>
        public bool CanComplete =>
            IsReady &&
            !_isBusy &&
            (ActiveQuest.Type == QuestType.ActionCounter ||
             ActiveQuest.Type == QuestType.LevelResult ||
             ActiveQuest.Type == QuestType.PlayerState &&
             CanCompletePlayerStateQuest(ActiveQuest)) &&
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
                Quest quest = quests[index];
                options[index] = quest.Category == QuestCategory.Daily
                    ? $"{quest.Definition.DailyDifficulty}: " +
                      FormatTitle(quest)
                    : FormatTitle(quest);
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
        /// Сбрасывает сохранённое состояние выбранного квеста и его тестируемой цели.
        /// </summary>
        public void ResetQuest()
        {
            if (!CanResetQuest)
            {
                return;
            }

            RunAction(
                "Reset Quest",
                () =>
                {
                    Quest quest = ActiveQuest;
                    // Возвращаем поддерживаемую внешнюю цель в исходное состояние.
                    if (quest.Type == QuestType.PlayerState)
                    {
                        ResetPlayerStateTarget(quest);
                    }

                    // Сбрасываем сохранённое состояние самого квеста.
                    if (!QuestManager.ResetQuestForTesting(quest.Id))
                    {
                        throw new InvalidOperationException(
                            "Выбранный квест не найден в QuestManager.");
                    }
                });
        }

        /// <summary>
        /// Переводит Daily и Story-квесты в следующий суточный период.
        /// </summary>
        public void AdvanceQuestDay()
        {
            if (!CanAdvanceQuestDay)
            {
                return;
            }

            RunAction(
                "Advance Quest Day",
                () =>
                {
                    if (!QuestManager.AdvanceQuestDayForTesting())
                    {
                        throw new InvalidOperationException(
                            "Следующий квестовый день не удалось создать.");
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
            Quest quest = ActiveQuest;
            if (quest.Type == QuestType.ActionCounter)
            {
                RunActionCounterAttempt(
                    quest,
                    "Advance",
                    partialProgress);
                return;
            }

            RunLevelResultAttempts(
                quest,
                "Advance",
                partialProgress);
        }

        /// <summary>
        /// Выполняет выбранный квест через его реальный игровой путь.
        /// </summary>
        public void Complete()
        {
            if (!CanComplete)
            {
                return;
            }

            Quest quest = ActiveQuest;
            switch (quest.Type)
            {
                case QuestType.ActionCounter:
                    int remainingProgress =
                        quest.TargetAmount - quest.CurrentProgress;
                    RunActionCounterAttempt(
                        quest,
                        "Complete",
                        remainingProgress);
                    return;
                case QuestType.LevelResult:
                    RunLevelResultQuest(quest);
                    return;
                case QuestType.PlayerState:
                    RunPlayerStateQuest(quest);
                    return;
                default:
                    throw new InvalidOperationException(
                        $"Тест-тул не поддерживает тип {quest.Type}.");
            }
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

        /// <summary>
        /// Обновляет выбор после штатной смены Daily-набора.
        /// </summary>
        public void HandleDailyQuestSetChanged()
        {
            HandleQuestSetChanged(QuestCategory.Daily);
        }

        /// <summary>
        /// Обновляет выбор после штатной смены Story-набора.
        /// </summary>
        public void HandleStoryQuestSetChanged()
        {
            HandleQuestSetChanged(QuestCategory.Story);
        }

        /// <summary>
        /// Нормализует выбранный квест после смены активного набора.
        /// </summary>
        private void HandleQuestSetChanged(QuestCategory category)
        {
            if (_selectedCategory != category)
            {
                return;
            }

            bool selectedQuestStillActive = AvailableQuests.Any(
                quest => quest.Id == _selectedQuestId);
            if (!selectedQuestStillActive)
            {
                _selectedQuestId = null;
            }

            if (_isBusy)
            {
                Changed?.Invoke();
                return;
            }

            ResetSelectionState();
        }

        private void RunActionCounterAttempt(
            Quest quest,
            string actionName,
            int actionCount)
        {
            RunQuestProgressAction(
                quest,
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

        private void RunLevelResultQuest(Quest quest)
        {
            if (quest.Definition.CountUniqueLevels)
            {
                RunQuestProgressAction(
                    quest,
                    "Complete",
                    () => CompleteUniqueLevelResultQuest(quest));
                return;
            }

            int remainingProgress =
                quest.TargetAmount - quest.CurrentProgress;
            RunLevelResultAttempts(
                quest,
                "Complete",
                remainingProgress);
        }

        private void RunLevelResultAttempts(
            Quest quest,
            string actionName,
            int completionCount)
        {
            RunQuestProgressAction(
                quest,
                actionName,
                () =>
                {
                    QuestDefinition definition = quest.Definition;
                    int progressBeforeAttempt = quest.CurrentProgress;
                    int levelId = definition.RequiredLevelId == 0
                        ? GetValidAttemptLevelId()
                        : definition.RequiredLevelId;

                    // Публикуем нужное количество успешных результатов уровней.
                    for (int index = 0; index < completionCount; index++)
                    {
                        int progressBeforeLevel = quest.CurrentProgress;
                        GameEventsManager.LevelStarted(levelId);
                        if (quest.CurrentProgress != progressBeforeLevel)
                        {
                            throw new InvalidOperationException(
                                "Прогресс изменился до победы.");
                        }

                        GameEventsManager.LevelCompleted(
                            levelId,
                            definition.RequiredStars);
                    }

                    // Проверяем точный прирост от опубликованных побед.
                    int expectedProgress = Math.Min(
                        progressBeforeAttempt + completionCount,
                        quest.TargetAmount);
                    if (quest.CurrentProgress != expectedProgress)
                    {
                        throw new InvalidOperationException(
                            $"Ожидался прогресс {expectedProgress}, " +
                            $"получен {quest.CurrentProgress}.");
                    }
                });
        }

        private void RunPlayerStateQuest(Quest quest)
        {
            RunQuestProgressAction(
                quest,
                "Complete",
                () =>
                {
                    // Выполняем реальное действие, связанное с PlayerState.
                    CompletePlayerStateTarget(quest.Definition);
                    // Проверяем результат через обновлённый runtime-квест.
                    if (!quest.IsCompleted)
                    {
                        throw new InvalidOperationException(
                            "Изменение состояния игрока не завершило квест.");
                    }
                });
        }

        private static bool CanResetPlayerStateQuest(Quest quest)
        {
            QuestDefinition definition = quest.Definition;
            switch (definition.StateId)
            {
                case PlayerStateIds.SkinOwned:
                    return TryGetPlayerStateSkin(definition, out Skin ownedSkin) &&
                           (!ownedSkin.IsPurchased || ownedSkin.Id > 0);
                case PlayerStateIds.SkinApplied:
                    return TryGetPlayerStateSkin(definition, out Skin appliedSkin) &&
                           (GameDataManager.PlayerData.AppliedSkinId !=
                            appliedSkin.Id ||
                            TryGetAlternativePurchasedSkin(
                                appliedSkin.Id,
                                out _));
                case PlayerStateIds.SuperAttackActive:
                    return TryGetPlayerStateEntityId(
                               definition,
                               out int superAttackId) &&
                           (GameDataManager.PlayerData.ActiveSuperAttackId !=
                            superAttackId ||
                            TryGetAlternativeSuperAttack(
                                superAttackId,
                                out _));
                default:
                    return false;
            }
        }

        private static bool CanCompletePlayerStateQuest(Quest quest)
        {
            QuestDefinition definition = quest.Definition;
            switch (definition.StateId)
            {
                case PlayerStateIds.SkinOwned:
                    return TryGetPlayerStateSkin(definition, out Skin ownedSkin) &&
                           !ownedSkin.IsPurchased;
                case PlayerStateIds.SkinApplied:
                    return TryGetPlayerStateSkin(definition, out Skin appliedSkin) &&
                           GameDataManager.PlayerData.AppliedSkinId !=
                           appliedSkin.Id;
                case PlayerStateIds.SuperAttackActive:
                    return TryGetPlayerStateEntityId(
                               definition,
                               out int superAttackId) &&
                           SuperAttackService.IsUnlocked(
                               superAttackId,
                               GameDataManager.PlayerData.PlayerLevel) &&
                           GameDataManager.PlayerData.ActiveSuperAttackId !=
                           superAttackId;
                case PlayerStateIds.PlayerLevel:
                    return definition.EntityId == PlayerStateEntityIds.Player &&
                           definition.RequiredValue >
                           GameDataManager.PlayerData.PlayerLevel;
                default:
                    return false;
            }
        }

        private static void ResetPlayerStateTarget(Quest quest)
        {
            QuestDefinition definition = quest.Definition;
            switch (definition.StateId)
            {
                case PlayerStateIds.SkinOwned:
                    ResetSkinOwnedTarget(definition);
                    return;
                case PlayerStateIds.SkinApplied:
                    ResetSkinAppliedTarget(definition);
                    return;
                case PlayerStateIds.SuperAttackActive:
                    ResetSuperAttackTarget(definition);
                    return;
            }
        }

        private static void ResetSkinOwnedTarget(QuestDefinition definition)
        {
            if (!TryGetPlayerStateSkin(definition, out Skin skin) ||
                !skin.IsPurchased)
            {
                return;
            }

            if (!SkinManager.ResetSkinPurchaseForTesting(skin.Id))
            {
                throw new InvalidOperationException(
                    "Целевой скин не удалось подготовить.");
            }
        }

        private static void ResetSkinAppliedTarget(
            QuestDefinition definition)
        {
            if (!TryGetPlayerStateSkin(definition, out Skin skin) ||
                GameDataManager.PlayerData.AppliedSkinId != skin.Id)
            {
                return;
            }

            if (!TryGetAlternativePurchasedSkin(
                    skin.Id,
                    out Skin alternativeSkin))
            {
                throw new InvalidOperationException(
                    "Нет другого купленного скина для сброса.");
            }

            SkinManager.PutOnSkin(alternativeSkin.Id);
        }

        private static void ResetSuperAttackTarget(
            QuestDefinition definition)
        {
            if (!TryGetPlayerStateEntityId(
                    definition,
                    out int superAttackId) ||
                GameDataManager.PlayerData.ActiveSuperAttackId !=
                superAttackId)
            {
                return;
            }

            if (!TryGetAlternativeSuperAttack(
                    superAttackId,
                    out int alternativeId) ||
                !SuperAttackService.TrySelect(alternativeId))
            {
                throw new InvalidOperationException(
                    "Нет другого доступного суперудара для сброса.");
            }
        }

        private void CompletePlayerStateTarget(QuestDefinition definition)
        {
            switch (definition.StateId)
            {
                case PlayerStateIds.SkinOwned:
                    CompleteSkinOwnedTarget(definition);
                    return;
                case PlayerStateIds.SkinApplied:
                    CompleteSkinOwnedTarget(definition);
                    SkinManager.PutOnSkin(ParsePlayerStateEntityId(definition));
                    return;
                case PlayerStateIds.SuperAttackActive:
                    if (!SuperAttackService.TrySelect(
                            ParsePlayerStateEntityId(definition)))
                    {
                        throw new InvalidOperationException(
                            "Суперудар не удалось выбрать.");
                    }

                    return;
                case PlayerStateIds.PlayerLevel:
                    CompletePlayerLevelTarget(definition);
                    return;
            }
        }

        private static void CompleteSkinOwnedTarget(
            QuestDefinition definition)
        {
            if (!TryGetPlayerStateSkin(definition, out Skin skin))
            {
                throw new InvalidOperationException(
                    $"Скин {definition.EntityId} не найден.");
            }

            // Готовим минимальный баланс и выполняем реальную покупку.
            int missingResource = Math.Max(
                0,
                skin.Price - ResourceManager.GetCurrentBalance(
                    skin.PriceType));
            if (missingResource > 0 &&
                !ResourceManager.AddResource(
                    skin.PriceType,
                    missingResource))
            {
                throw new InvalidOperationException(
                    "Не удалось подготовить ресурсы для покупки скина.");
            }

            SkinManager.PurchaseSkin(skin.Id);
        }

        private void CompletePlayerLevelTarget(QuestDefinition definition)
        {
            // Начисляем штатный Story XP до заданного Player Level.
            while (GameDataManager.PlayerData.PlayerLevel <
                   definition.RequiredValue)
            {
                _playerExperienceService
                    .GrantExperienceForClaimedStorylineQuest(
                        GameDataManager.PlayerData);
            }

            // Сохраняем весь dev-прогон одним checkpoint.
            PlayerProgressCommitter.Commit(
                CheckpointReason.QuestProgressed);
        }

        private static bool TryGetPlayerStateSkin(
            QuestDefinition definition,
            out Skin skin)
        {
            skin = null;
            if (!TryGetPlayerStateEntityId(definition, out int skinId))
            {
                return false;
            }

            skin = SkinManager.AvailableSkins.FirstOrDefault(
                availableSkin => availableSkin.Id == skinId);
            return skin != null;
        }

        private static bool TryGetAlternativePurchasedSkin(
            int excludedSkinId,
            out Skin skin)
        {
            skin = SkinManager.AvailableSkins.FirstOrDefault(
                availableSkin =>
                    availableSkin.Id != excludedSkinId &&
                    availableSkin.IsPurchased);
            return skin != null;
        }

        private static bool TryGetAlternativeSuperAttack(
            int excludedSuperAttackId,
            out int superAttackId)
        {
            SuperAttackData superAttack = SuperAttackService.Items
                .FirstOrDefault(item =>
                    item.Id != excludedSuperAttackId &&
                    SuperAttackService.IsUnlocked(
                        item.Id,
                        GameDataManager.PlayerData.PlayerLevel));
            superAttackId = superAttack?.Id ?? 0;
            return superAttack != null;
        }

        private static int ParsePlayerStateEntityId(
            QuestDefinition definition)
        {
            if (TryGetPlayerStateEntityId(definition, out int entityId))
            {
                return entityId;
            }

            throw new InvalidOperationException(
                $"Некорректный ID цели {definition.EntityId}.");
        }

        private static bool TryGetPlayerStateEntityId(
            QuestDefinition definition,
            out int entityId)
        {
            return int.TryParse(definition.EntityId, out entityId);
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

        /// <summary>
        /// Проводит прогресс через QuestManager только для выбранного квеста.
        /// </summary>
        private void RunQuestProgressAction(
            Quest quest,
            string actionName,
            Action action)
        {
            RunAction(
                actionName,
                () => QuestManager.RunQuestProgressForTesting(
                    quest.Id,
                    action));
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
                    GameEventsManager.ObstacleJumpedOnFromRoof(sourceId);
                    break;
                case GameplayActionIds.VehicleRoofRunCompleted:
                    GameEventsManager.VehicleRoofRunCompleted();
                    break;
                case GameplayActionIds.RoofToRoofJump:
                    GameEventsManager.RoofToRoofJump();
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
            return $"{QuestTitleFormatter.Format(quest)} ({quest.Id})";
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
