using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.System;
using GameManagement;
using GameManagement.Progress;
using Vues.GameCore.Quests;

namespace Vues.GameCore
{
    /// <summary>
    /// Связывает активные квесты с событиями игры, сейвом и UI.
    /// </summary>
    public static class QuestManager
    {
        /// <summary>
        /// Идентификатор устаревшего сюжетного квеста для очистки старых сохранений.
        /// </summary>
        private const string LegacyStoryQuestId = "story-001";

        /// <summary>
        /// Стратегии обработки прогресса для каждого типа квеста.
        /// </summary>
        private static readonly IReadOnlyDictionary<QuestType, IQuestStrategy>
            _strategies = new Dictionary<QuestType, IQuestStrategy>
            {
                [QuestType.ActionCounter] =
                    new ActionCounterQuestStrategy(),
                [QuestType.LevelResult] =
                    new LevelResultQuestStrategy(),
                [QuestType.PlayerState] =
                    new PlayerStateQuestStrategy()
            };

        /// <summary>
        /// Буфер событий текущей попытки прохождения уровня.
        /// </summary>
        private static readonly QuestAttemptBuffer _attemptBuffer = new();

        /// <summary>
        /// Управляет созданием, сменой и общей наградой Daily-набора.
        /// </summary>
        private static readonly DailyQuestService _dailyQuestService = new(
            new DailyQuestGenerator(),
            new DailyQuestScheduler());

        /// <summary>
        /// Начисляет опыт за полученные квестовые награды.
        /// </summary>
        private static readonly PlayerExperienceService
            _playerExperienceService = new();

        /// <summary>
        /// Создаёт и восстанавливает определения Story-квестов.
        /// </summary>
        private static StoryQuestGenerator _storyQuestGenerator;

        /// <summary>
        /// Все активные квесты для обработки игровых событий.
        /// </summary>
        private static IReadOnlyList<Quest> _activeQuests =
            Array.Empty<Quest>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Ограничивает синхронные игровые события Quest Testing выбранным квестом.
        /// </summary>
        private static string _questIdUnderTest;
#endif

        /// <summary>
        /// Активные runtime-состояния Daily-квестов.
        /// </summary>
        private static IReadOnlyList<Quest> _dailyQuests =
            Array.Empty<Quest>();

        /// <summary>
        /// Активные runtime-состояния Story-квестов.
        /// </summary>
        private static IReadOnlyList<Quest> _storyQuests =
            Array.Empty<Quest>();

        /// <summary>
        /// Определения квестов в текущих Story-слотах.
        /// </summary>
        private static IReadOnlyList<QuestDefinition>
            _storyQuestDefinitions = Array.Empty<QuestDefinition>();

        #region Daily-набор

        /// <summary>
        /// Активные дневные квесты текущего набора.
        /// </summary>
        public static IReadOnlyList<Quest> DailyQuests =>
            _dailyQuests;

        /// <summary>
        /// Проверяет смену локального дня и обновляет активные наборы.
        /// </summary>
        public static void Update()
        {
            if (!_dailyQuestService.IsInitialized)
            {
                return;
            }

            // Обновляем сохранённый набор после наступления нового дня.
            List<string> previousIds =
                _dailyQuestService.State.ActiveQuestIds.ToList();
            if (!_dailyQuestService.Update(
                    DateTime.Now,
                    GameDataManager.PlayerData.QuestStates))
            {
                return;
            }

            CompleteQuestDayChange(previousIds);
        }

        /// <summary>
        /// Применяет и публикует суточную смену активных квестов.
        /// </summary>
        private static void CompleteQuestDayChange(
            IReadOnlyCollection<string> previousIds)
        {
            // Обновляем полученные Story-квесты вместе с Daily-набором.
            StoryQuestSetState previousStoryState =
                GameDataManager.PlayerData.StoryQuestSet;
            string previousPrimaryQuestId =
                previousStoryState?.ActivePrimaryQuestId;
            string previousSecondaryQuestId =
                previousStoryState?.ActiveSecondaryQuestId;
            InitStoryQuestSet();
            StoryQuestSetState currentStoryState =
                GameDataManager.PlayerData.StoryQuestSet;
            bool storySetChanged =
                !string.Equals(
                    previousPrimaryQuestId,
                    currentStoryState.ActivePrimaryQuestId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    previousSecondaryQuestId,
                    currentStoryState.ActiveSecondaryQuestId,
                    StringComparison.Ordinal);

            // Переподключаем активные квесты и сохраняем оба набора.
            ApplyDailySetChange(previousIds, hadGeneratedSet: true);
            BindActiveQuests(discardAttempt: false);
            PlayerProgressCommitter.Commit(
                CheckpointReason.DailyQuestSetRotated);

            // Уведомляем открытые экраны об изменении набора.
            GameEventsManager.DailyQuestSetChanged();
            if (storySetChanged)
            {
                GameEventsManager.StoryQuestSetChanged();
            }
        }

        /// <summary>
        /// Восстанавливает или создаёт Daily-набор на указанную дату.
        /// </summary>
        private static bool InitDailyQuestSet(DateTime localNow)
        {
            // Сохраняем прежний набор для очистки устаревшего прогресса.
            DailyQuestSetState savedState =
                GameDataManager.PlayerData.DailyQuestSet;
            bool hadGeneratedSet =
                !string.IsNullOrWhiteSpace(savedState?.GenerationDate);
            List<string> previousIds =
                savedState?.ActiveQuestIds?.ToList() ?? new List<string>();

            // Подключаем сохранённое состояние и создаём набор при необходимости.
            bool changed = _dailyQuestService.Init(
                QuestCatalog.DailyDefinitions,
                savedState,
                GameDataManager.PlayerData.QuestStates,
                localNow);
            GameDataManager.PlayerData.DailyQuestSet =
                _dailyQuestService.State;

            // Удаляем состояния квестов, покинувших активный набор.
            if (changed)
            {
                ApplyDailySetChange(previousIds, hadGeneratedSet);
            }

            return changed;
        }

        /// <summary>
        /// Очищает прогресс покинувших Daily-набор квестов.
        /// </summary>
        private static void ApplyDailySetChange(
            IReadOnlyCollection<string> previousIds,
            bool hadGeneratedSet)
        {
            // Первый набор очищает весь старый Daily-прогресс.
            IEnumerable<string> expiredQuestIds =
                QuestCatalog.DailyDefinitions.Select(
                    definition => definition.Id);

            // Следующий набор сохраняет только защищённые квесты.
            if (hadGeneratedSet)
            {
                expiredQuestIds = previousIds.Where(questId =>
                    !_dailyQuestService.RetainsProgress(questId));
            }

            var expiredIds = new HashSet<string>(
                expiredQuestIds,
                StringComparer.Ordinal);

            // Удаляем покинувшие набор состояния и публикуем новый набор.
            GameDataManager.PlayerData.QuestStates.RemoveAll(
                quest => quest != null &&
                         expiredIds.Contains(quest.QuestId));
            GameDataManager.PlayerData.DailyQuestSet =
                _dailyQuestService.State;
        }

        /// <summary>
        /// Возвращает определения активного Daily-набора в сохранённом порядке.
        /// </summary>
        private static IReadOnlyList<QuestDefinition>
            ResolveActiveDailyDefinitions()
        {
            var definitions = new List<QuestDefinition>(
                _dailyQuestService.State.ActiveQuestIds.Count);
            foreach (string questId in
                     _dailyQuestService.State.ActiveQuestIds)
            {
                if (!QuestCatalog.TryGet(
                        questId,
                        out QuestDefinition definition) ||
                    definition.Category != QuestCategory.Daily)
                {
                    throw new InvalidOperationException(
                        $"Дневной квест {questId} отсутствует в каталоге.");
                }

                definitions.Add(definition);
            }

            return definitions.AsReadOnly();
        }

        #endregion

        #region Story-набор

        /// <summary>
        /// Активные сюжетные квесты текущего набора.
        /// </summary>
        public static IReadOnlyList<Quest> StoryQuests =>
            _storyQuests;

        /// <summary>
        /// Восстанавливает или создаёт оба Story-слота.
        /// </summary>
        private static bool InitStoryQuestSet()
        {
            if (_storyQuestGenerator == null)
            {
                throw new InvalidOperationException(
                    "Генератор сюжетных квестов не инициализирован.");
            }

            PlayerData playerData = GameDataManager.PlayerData ??
                throw new InvalidOperationException(
                    "Данные игрока недоступны для Story-генерации.");

            // Удаляем старый формат квеста и нормализуем сохранённые слоты.
            playerData.QuestStates ??= new List<Quest>();
            bool legacyStateRemoved = playerData.QuestStates.RemoveAll(
                quest => quest != null &&
                         string.Equals(
                             quest.QuestId,
                             LegacyStoryQuestId,
                             StringComparison.Ordinal)) > 0;
            StoryQuestSetState savedState = playerData.StoryQuestSet;
            StoryQuestSetState state =
                savedState ?? new StoryQuestSetState();
            state.GenerationDate ??= string.Empty;
            state.ActivePrimaryQuestId ??= string.Empty;
            state.ActiveSecondaryQuestId ??= string.Empty;
            string currentGenerationDate =
                _dailyQuestService.State.GenerationDate;
            string previousGenerationDate = state.GenerationDate;
            string previousPrimaryQuestId =
                state.ActivePrimaryQuestId;
            string previousSecondaryQuestId =
                state.ActiveSecondaryQuestId;
            bool rotateClaimedQuests =
                !string.Equals(
                    previousGenerationDate,
                    currentGenerationDate,
                    StringComparison.Ordinal);
            if (string.Equals(
                    state.ActivePrimaryQuestId,
                    LegacyStoryQuestId,
                    StringComparison.Ordinal))
            {
                state.ActivePrimaryQuestId = string.Empty;
            }

            if (string.Equals(
                    state.ActiveSecondaryQuestId,
                    LegacyStoryQuestId,
                    StringComparison.Ordinal))
            {
                state.ActiveSecondaryQuestId = string.Empty;
            }

            LevelProgressOverview progressOverview =
                LevelManager.SavedProgressOverview;
            var definitions = new List<QuestDefinition>(2);

            // Восстанавливаем последовательный слот или создаём его заново.
            QuestDefinition primaryDefinition =
                ResolvePrimaryStoryDefinition(
                    GetRestorableStoryQuestId(
                        playerData,
                        state.ActivePrimaryQuestId,
                        rotateClaimedQuests),
                    progressOverview,
                    playerData);
            state.ActivePrimaryQuestId =
                primaryDefinition?.Id ?? string.Empty;
            if (primaryDefinition != null)
            {
                definitions.Add(primaryDefinition);
            }

            // Восстанавливаем случайный слот или выбираем новую доступную цель.
            QuestDefinition secondaryDefinition =
                ResolveSecondaryStoryDefinition(
                    GetRestorableStoryQuestId(
                        playerData,
                        state.ActiveSecondaryQuestId,
                        rotateClaimedQuests),
                    progressOverview,
                    playerData);
            state.ActiveSecondaryQuestId =
                secondaryDefinition?.Id ?? string.Empty;
            if (secondaryDefinition != null)
            {
                definitions.Add(secondaryDefinition);
            }

            // Публикуем нормализованное состояние и определения двух слотов.
            state.GenerationDate = currentGenerationDate;
            playerData.StoryQuestSet = state;
            _storyQuestDefinitions = definitions.AsReadOnly();
            return legacyStateRemoved ||
                   savedState == null ||
                   !string.Equals(
                       previousGenerationDate,
                       state.GenerationDate,
                       StringComparison.Ordinal) ||
                   !string.Equals(
                       previousPrimaryQuestId,
                       state.ActivePrimaryQuestId,
                       StringComparison.Ordinal) ||
                   !string.Equals(
                       previousSecondaryQuestId,
                       state.ActiveSecondaryQuestId,
                       StringComparison.Ordinal);
        }

        /// <summary>
        /// Восстанавливает или создаёт последовательный Story-квест.
        /// </summary>
        private static QuestDefinition ResolvePrimaryStoryDefinition(
            string savedQuestId,
            LevelProgressOverview progressOverview,
            PlayerData playerData)
        {
            // Стабильный сохранённый слот имеет приоритет над новой целью.
            if (_storyQuestGenerator.TryRestorePrimaryDefinition(
                    savedQuestId,
                    progressOverview,
                    out QuestDefinition definition))
            {
                return definition;
            }

            // Пустой или устаревший слот заполняем по текущему прогрессу.
            if (!_storyQuestGenerator.TryCreatePrimaryDefinition(
                    progressOverview,
                    out definition))
            {
                return null;
            }

            return IsQuestRewardClaimed(playerData, definition.Id)
                ? null
                : definition;
        }

        /// <summary>
        /// Проверяет, была ли уже получена награда указанного квеста.
        /// </summary>
        private static bool IsQuestRewardClaimed(
            PlayerData playerData,
            string questId)
        {
            return !string.IsNullOrWhiteSpace(questId) &&
                   playerData.QuestStates?.Any(quest =>
                       quest != null &&
                       string.Equals(
                           quest.QuestId,
                           questId,
                           StringComparison.Ordinal) &&
                       quest.IsRewardClaimed) == true;
        }

        /// <summary>
        /// Возвращает Story-квест для восстановления в текущем суточном периоде.
        /// </summary>
        private static string GetRestorableStoryQuestId(
            PlayerData playerData,
            string questId,
            bool rotateClaimedQuests)
        {
            return rotateClaimedQuests &&
                   IsQuestRewardClaimed(playerData, questId)
                ? string.Empty
                : questId;
        }

        /// <summary>
        /// Восстанавливает или создаёт случайный Story-квест.
        /// </summary>
        private static QuestDefinition ResolveSecondaryStoryDefinition(
            string savedQuestId,
            LevelProgressOverview progressOverview,
            PlayerData playerData)
        {
            // Стабильный сохранённый слот имеет приоритет над случайным выбором.
            if (_storyQuestGenerator.TryRestoreSecondaryDefinition(
                    savedQuestId,
                    progressOverview,
                    SkinManager.AvailableSkins,
                    SuperAttackService.Items,
                    out QuestDefinition definition))
            {
                return definition;
            }

            // Пустой или устаревший слот заполняем доступной случайной целью.
            return _storyQuestGenerator.TryCreateSecondaryDefinition(
                progressOverview,
                playerData,
                SkinManager.AvailableSkins,
                SuperAttackService.Items,
                out definition)
                    ? definition
                    : null;
        }

        #endregion

        #region Инициализация и runtime-состояние

        /// <summary>
        /// Загружает каталог, восстанавливает или создаёт активные квесты.
        /// </summary>
        public static async Task Init()
        {
            // Загружаем проверенный production-каталог.
            await QuestCatalog.LoadAsync();

            // Восстанавливаем оба набора и связываем их runtime-состояния.
            _storyQuestGenerator = new StoryQuestGenerator(
                QuestCatalog.StoryGenerationSettings);
            bool dailySetChanged = InitDailyQuestSet(DateTime.Now);
            bool storySetChanged = InitStoryQuestSet();
            bool questStatesChanged = BindActiveQuests();

            // Сохраняем созданные или исправленные наборы одним checkpoint.
            if (dailySetChanged || storySetChanged || questStatesChanged)
            {
                PlayerProgressCommitter.Commit(
                    dailySetChanged
                        ? CheckpointReason.DailyQuestSetRotated
                        : storySetChanged
                            ? CheckpointReason.StoryQuestSetChanged
                            : CheckpointReason.QuestProgressed);
            }
        }

        /// <summary>
        /// Связывает активные определения с состояниями игрока.
        /// </summary>
        private static bool BindActiveQuests(bool discardAttempt = true)
        {
            // Подключаем выбранные Daily и два сохранённых Story-слота.
            _dailyQuests = BindDefinitions(
                ResolveActiveDailyDefinitions(),
                out bool dailyStatesChanged);
            _storyQuests = BindDefinitions(
                _storyQuestDefinitions,
                out bool storyStatesChanged);

            // Собираем единый список для обработки игровых событий.
            var activeQuests = new List<Quest>(
                _dailyQuests.Count + _storyQuests.Count);
            activeQuests.AddRange(_dailyQuests);
            activeQuests.AddRange(_storyQuests);
            _activeQuests = activeQuests.AsReadOnly();

            // Полная переинициализация завершает старую попытку.
            if (discardAttempt)
            {
                _attemptBuffer.DiscardAttempt();
            }

            return dailyStatesChanged || storyStatesChanged;
        }

        /// <summary>
        /// Создаёт runtime-квесты и восстанавливает их сохранённый прогресс.
        /// </summary>
        private static IReadOnlyList<Quest> BindDefinitions(
            IReadOnlyList<QuestDefinition> definitions,
            out bool stateChanged)
        {
            stateChanged = false;
            var quests = new List<Quest>(definitions.Count);
            foreach (QuestDefinition definition in definitions)
            {
                // Проверяем runtime-стратегию до изменения состояния игрока.
                if (!_strategies.TryGetValue(
                        definition.Type,
                        out IQuestStrategy strategy))
                {
                    throw new InvalidOperationException(
                        $"Стратегия типа {definition.Type} не подключена.");
                }

                // Подключаем описание и восстанавливаем сохранённый прогресс.
                Quest quest = GetOrCreateQuest(
                    definition.Id,
                    out bool stateCreated);
                int previousProgress = quest.CurrentProgress;
                bool wasCompleted = quest.IsCompleted;
                bool wasRewardClaimed = quest.IsRewardClaimed;
                bool hadCountedLevelKeys =
                    quest.CountedLevelKeys != null;
                int previousCountedLevelCount =
                    quest.CountedLevelKeys?.Count ?? 0;
                quest.Bind(definition, strategy);
                bool restoredProgress = RestoreUniqueLevelProgress(quest);
                restoredProgress |= RestorePlayerStateProgress(quest);
                stateChanged |= stateCreated ||
                                restoredProgress ||
                                previousProgress != quest.CurrentProgress ||
                                wasCompleted != quest.IsCompleted ||
                                wasRewardClaimed != quest.IsRewardClaimed ||
                                hadCountedLevelKeys !=
                                (quest.CountedLevelKeys != null) ||
                                previousCountedLevelCount !=
                                (quest.CountedLevelKeys?.Count ?? 0);
                quests.Add(quest);
            }

            return quests.AsReadOnly();
        }

        /// <summary>
        /// Восстанавливает прогресс квеста по уникальным пройденным уровням.
        /// </summary>
        private static bool RestoreUniqueLevelProgress(Quest quest)
        {
            QuestDefinition definition = quest.Definition;
            if (!definition.CountUniqueLevels)
            {
                return false;
            }

            LevelProgressOverview progressOverview =
                LevelManager.SavedProgressOverview;
            IReadOnlyList<LevelProgress> levels;

            // Выбираем агрегат заданной части или полный прогресс игрока.
            if (!string.IsNullOrWhiteSpace(definition.RequiredLocationId) &&
                !string.IsNullOrWhiteSpace(definition.RequiredPartOfDayId))
            {
                if (!progressOverview.TryGetPart(
                        definition.RequiredLocationId,
                        definition.RequiredPartOfDayId,
                        out PartProgress part))
                {
                    return false;
                }

                levels = part.Levels;
            }
            else
            {
                levels = progressOverview.Levels;
            }

            // Применяем подходящие сохранённые результаты к квесту.
            bool stateChanged = false;
            foreach (LevelProgress level in levels)
            {
                if (level.Stars < definition.RequiredStars)
                {
                    continue;
                }

                stateChanged |= quest.Handle(
                    new LevelResultQuestEvent(
                        level.LevelNumber,
                        level.Stars,
                        level.Key.ToString(),
                        level.Key.LocationId,
                        level.Key.PartOfDayId));
            }

            return stateChanged;
        }

        /// <summary>
        /// Восстанавливает прогресс квеста по текущему состоянию игрока.
        /// </summary>
        private static bool RestorePlayerStateProgress(Quest quest)
        {
            QuestDefinition definition = quest.Definition;
            if (definition.Type != QuestType.PlayerState ||
                !PlayerStateValueResolver.TryGetCurrentValue(
                    GameDataManager.PlayerData,
                    definition.StateId,
                    definition.EntityId,
                    out int value))
            {
                return false;
            }

            return quest.Handle(
                new PlayerStateQuestEvent(
                    definition.StateId,
                    definition.EntityId,
                    value));
        }

        /// <summary>
        /// Возвращает сохранённый квест или создаёт его состояние.
        /// </summary>
        private static Quest GetOrCreateQuest(
            string questId,
            out bool created)
        {
            GameDataManager.PlayerData.QuestStates ??=
                new List<Quest>();
            Quest quest = GameDataManager.PlayerData.QuestStates
                .FirstOrDefault(savedQuest =>
                    savedQuest.QuestId == questId);
            if (quest != null)
            {
                created = false;
                return quest;
            }

            quest = new Quest
            {
                QuestId = questId
            };
            GameDataManager.PlayerData.QuestStates.Add(quest);
            created = true;
            return quest;
        }

        #endregion

        #region Игровые события и замена профиля

        /// <summary>
        /// Подключает обработчики игровых событий.
        /// </summary>
        public static void OnEnable()
        {
            GameEventsManager.OnLevelStarted += HandleLevelStarted;
            GameEventsManager.OnActionCounterQuestEvent +=
                HandleActionCounterQuestEvent;
            GameEventsManager.OnLevelCompleted += HandleLevelCompleted;
            GameEventsManager.OnPlayerStateChanged +=
                HandlePlayerStateChanged;
            GameDataManager.PlayerDataReplaced += HandlePlayerDataReplaced;
        }

        /// <summary>
        /// Отключает обработчики и очищает текущую попытку.
        /// </summary>
        public static void OnDisable()
        {
            GameEventsManager.OnLevelStarted -= HandleLevelStarted;
            GameEventsManager.OnActionCounterQuestEvent -=
                HandleActionCounterQuestEvent;
            GameEventsManager.OnLevelCompleted -= HandleLevelCompleted;
            GameEventsManager.OnPlayerStateChanged -=
                HandlePlayerStateChanged;
            GameDataManager.PlayerDataReplaced -= HandlePlayerDataReplaced;
            _attemptBuffer.DiscardAttempt();
        }

        /// <summary>
        /// Пересобирает активные квесты после замены данных игрока.
        /// </summary>
        private static void HandlePlayerDataReplaced()
        {
            if (_activeQuests.Count == 0)
            {
                return;
            }

            // Пересобираем оба набора поверх нового состояния игрока.
            bool dailySetChanged = InitDailyQuestSet(DateTime.Now);
            bool storySetChanged = InitStoryQuestSet();
            bool questStatesChanged = BindActiveQuests();

            // Сохраняем исправленные наборы одним checkpoint.
            if (dailySetChanged || storySetChanged || questStatesChanged)
            {
                PlayerProgressCommitter.Commit(
                    dailySetChanged
                        ? CheckpointReason.DailyQuestSetRotated
                        : storySetChanged
                            ? CheckpointReason.StoryQuestSetChanged
                            : CheckpointReason.QuestProgressed);
            }

            // Полная замена профиля всегда требует перестроить оба UI-набора.
            GameEventsManager.DailyQuestSetChanged();
            GameEventsManager.StoryQuestSetChanged();

            foreach (Quest quest in _activeQuests)
            {
                GameEventsManager.QuestStateChanged(quest.Id);
            }
        }

        /// <summary>
        /// Добавляет действие игрока в текущую попытку уровня.
        /// </summary>
        private static void HandleActionCounterQuestEvent(
            ActionCounterQuestEvent questEvent)
        {
            _attemptBuffer.Add(questEvent);
        }

        /// <summary>
        /// Обновляет квесты после изменения состояния игрока.
        /// </summary>
        private static void HandlePlayerStateChanged(
            string stateId,
            string entityId)
        {
            if (!PlayerStateValueResolver.TryGetCurrentValue(
                    GameDataManager.PlayerData,
                    stateId,
                    entityId,
                    out int value))
            {
                return;
            }

            // Применяем состояние без буфера; сохраняет его владелец изменения.
            var questEvent = new PlayerStateQuestEvent(
                stateId,
                entityId,
                value);
            foreach (Quest quest in _activeQuests)
            {
                if (!ShouldHandleProgress(quest))
                {
                    continue;
                }

                bool wasCompleted = quest.IsCompleted;
                if (!quest.Handle(questEvent))
                {
                    continue;
                }

                if (!wasCompleted && quest.IsCompleted)
                {
                    GameEventsManager.QuestCompleted(quest.Id);
                }

                GameEventsManager.QuestStateChanged(quest.Id);
            }
        }

        /// <summary>
        /// Начинает сбор квестовых событий новой попытки.
        /// </summary>
        private static void HandleLevelStarted(int _)
        {
            _attemptBuffer.StartAttempt();
        }

        /// <summary>
        /// Применяет результат завершённой попытки к активным квестам.
        /// </summary>
        private static void HandleLevelCompleted(int levelId, int stars)
        {
            // Собираем итог завершённой попытки.
            IReadOnlyList<ActionCounterQuestEvent> bufferedEvents =
                _attemptBuffer.CompleteAttempt();
            bool hasProgressKey =
                LevelManager.TryGetCurrentProgressKey(
                    out LevelProgressKey progressKey);
            var levelResultEvent =
                new LevelResultQuestEvent(
                    levelId,
                    stars,
                    hasProgressKey ? progressKey.ToString() : string.Empty,
                    hasProgressKey ? progressKey.LocationId : string.Empty,
                    hasProgressKey ? progressKey.PartOfDayId : string.Empty);
            var changedQuests = new List<Quest>();
            var completedQuests = new List<Quest>();

            // Применяем факты завершённой попытки ко всем активным квестам.
            foreach (Quest quest in _activeQuests)
            {
                if (!ShouldHandleProgress(quest))
                {
                    continue;
                }

                bool wasCompleted = quest.IsCompleted;
                bool progressChanged = quest.Handle(levelResultEvent);
                foreach (ActionCounterQuestEvent questEvent in bufferedEvents)
                {
                    progressChanged |= quest.Handle(questEvent);
                }

                if (!progressChanged)
                {
                    continue;
                }

                changedQuests.Add(quest);
                if (!wasCompleted && quest.IsCompleted)
                {
                    completedQuests.Add(quest);
                }
            }

            if (changedQuests.Count == 0)
            {
                return;
            }

            // Сохраняем все изменения попытки одним checkpoint.
            PlayerProgressCommitter.Commit(
                completedQuests.Count > 0
                    ? CheckpointReason.QuestCompleted
                    : CheckpointReason.QuestProgressed);

            // Уведомляем о завершении и любом изменённом состоянии.
            foreach (Quest quest in completedQuests)
            {
                GameEventsManager.QuestCompleted(quest.Id);
            }

            foreach (Quest quest in changedQuests)
            {
                GameEventsManager.QuestStateChanged(quest.Id);
            }
        }

        /// <summary>
        /// Проверяет, может ли квест получить текущее игровое событие.
        /// </summary>
        private static bool ShouldHandleProgress(Quest quest)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!string.IsNullOrWhiteSpace(_questIdUnderTest))
            {
                return string.Equals(
                    quest.Id,
                    _questIdUnderTest,
                    StringComparison.Ordinal);
            }
#endif

            return true;
        }

        #endregion

        #region Инструменты разработки

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Переводит Daily и Story-квесты в следующий суточный период.
        /// </summary>
        public static bool AdvanceQuestDayForTesting()
        {
            if (!_dailyQuestService.IsInitialized)
            {
                return false;
            }

            List<string> previousIds =
                _dailyQuestService.State.ActiveQuestIds.ToList();
            if (!_dailyQuestService.GenerateNextSetForTesting(
                    GameDataManager.PlayerData.QuestStates))
            {
                return false;
            }

            CompleteQuestDayChange(previousIds);
            return true;
        }

        /// <summary>
        /// Проводит прогресс выбранного квеста, не изменяя другие активные квесты.
        /// </summary>
        public static void RunQuestProgressForTesting(
            string questId,
            Action progressAction)
        {
            if (string.IsNullOrWhiteSpace(questId))
            {
                throw new ArgumentException(
                    "Quest ID is required.",
                    nameof(questId));
            }

            if (progressAction == null)
            {
                throw new ArgumentNullException(nameof(progressAction));
            }

            if (!_activeQuests.Any(quest => quest.Id == questId))
            {
                throw new InvalidOperationException(
                    $"Active quest {questId} was not found.");
            }

            if (!string.IsNullOrWhiteSpace(_questIdUnderTest))
            {
                throw new InvalidOperationException(
                    "Another isolated quest progress action is already running.");
            }

            _questIdUnderTest = questId;
            try
            {
                progressAction();
            }
            finally
            {
                _questIdUnderTest = null;
                _attemptBuffer.DiscardAttempt();
            }
        }

        /// <summary>
        /// Сбрасывает выбранный активный квест для dev-тестирования.
        /// </summary>
        public static bool ResetQuestForTesting(string questId)
        {
            Quest quest = _activeQuests.FirstOrDefault(
                activeQuest => activeQuest.Id == questId);
            if (quest == null)
            {
                return false;
            }

            quest.Reset();
            _attemptBuffer.DiscardAttempt();
            GameDataManager.SaveData();
            GameEventsManager.QuestStateChanged(quest.Id);
            return true;
        }
#endif

        #endregion

        #region Награды

        /// <summary>
        /// Тип общей награды активного Daily-набора.
        /// </summary>
        public static ResourceType DailyCommonRewardType =>
            QuestCatalog.DailyCommonRewardDefinition?.RewardType ?? default;

        /// <summary>
        /// Размер общей награды активного Daily-набора.
        /// </summary>
        public static int DailyCommonRewardAmount =>
            QuestCatalog.DailyCommonRewardDefinition?.RewardAmount ?? 0;

        /// <summary>
        /// Признак готовности общей награды Daily-набора к получению.
        /// </summary>
        public static bool CanClaimDailyCommonReward =>
            _dailyQuestService.CanClaimCommonReward(_dailyQuests);

        /// <summary>
        /// Выдаёт одноразовую общую награду завершённого Daily-набора.
        /// </summary>
        public static bool ClaimDailyCommonReward()
        {
            // Проверяем конфигурацию и готовность активного набора.
            DailyCommonRewardDefinition definition =
                QuestCatalog.DailyCommonRewardDefinition;
            if (definition == null)
            {
                return false;
            }

            if (!_dailyQuestService.CanClaimCommonReward(_dailyQuests))
            {
                return false;
            }

            // Добавляем ресурс и фиксируем одноразовость награды.
            if (!ResourceManager.AddResource(
                    definition.RewardType,
                    definition.RewardAmount))
            {
                return false;
            }

            _dailyQuestService.MarkCommonRewardClaimed();

            // Сохраняем состояние перед уведомлением UI и экономики.
            PlayerProgressCommitter.Commit(
                CheckpointReason.DailyQuestCommonRewardClaimed);
            NotifyEarnedResource(
                definition.RewardType,
                definition.RewardAmount);
            return true;
        }

        /// <summary>
        /// Выдаёт награду завершённого активного квеста один раз.
        /// </summary>
        public static bool ClaimReward(string questId)
        {
            // Проверяем активный квест и добавляем его ресурсную награду.
            Quest quest = _activeQuests.FirstOrDefault(
                activeQuest => activeQuest.Id == questId);
            if (quest == null || !quest.CanClaimReward)
            {
                return false;
            }

            bool rewardAdded = ResourceManager.AddResource(
                quest.RewardType,
                quest.RewardAmount);
            if (!rewardAdded)
            {
                return false;
            }

            // Фиксируем награду и опыт выбранной категории.
            quest.MarkRewardClaimed();
            if (quest.Category == QuestCategory.Daily)
            {
                _playerExperienceService.GrantExperienceForClaimedDailyQuest(
                    GameDataManager.PlayerData);
            }
            else if (quest.Category == QuestCategory.Story)
            {
                _playerExperienceService
                    .GrantExperienceForClaimedStorylineQuest(
                        GameDataManager.PlayerData);
            }

            // Сохраняем результат перед уведомлением UI и экономики.
            PlayerProgressCommitter.Commit(
                CheckpointReason.QuestRewardClaimed);

            NotifyEarnedResource(
                quest.RewardType,
                quest.RewardAmount);
            GameEventsManager.QuestRewardReceived(questId);
            GameEventsManager.QuestStateChanged(questId);

            return true;
        }

        /// <summary>
        /// Уведомляет экономику о полученном квестовом ресурсе.
        /// </summary>
        private static void NotifyEarnedResource(
            ResourceType resourceType,
            int amount)
        {
            switch (resourceType)
            {
                case ResourceType.Coins:
                    GameEventsManager.EarnCoins(amount);
                    break;
                case ResourceType.Crystals:
                    GameEventsManager.EarnCrystals(amount);
                    break;
            }
        }

        #endregion
    }
}
