using System;
using System.Collections.Generic;
using System.Linq;

namespace Vues.GameCore.Quests
{
    /// <summary>
    /// Управляет сохранённым набором дневных квестов.
    /// </summary>
    public sealed class DailyQuestService
    {
        private const int DailyQuestCount = 3;

        private readonly DailyQuestGenerator _generator;
        private readonly DailyQuestScheduler _scheduler;

        private IReadOnlyList<QuestDefinition> _dailyPool =
            Array.Empty<QuestDefinition>();
        private bool _hasValidActiveSet;
        private DateTime _nextGenerationTime;
        private DailyCommonRewardDefinition _commonReward;
        private HashSet<string> _retainedQuestIds =
            new(StringComparer.Ordinal);

        /// <summary>
        /// Текущее сохранённое состояние набора.
        /// </summary>
        public DailyQuestSetState State { get; private set; }

        /// <summary>
        /// Признак готовности сервиса к проверке нового дня.
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>Проверяет смену дня до начала транзакции сохранения.</summary>
        public bool NeedsUpdate(DateTime localNow) => IsInitialized &&
            (!_hasValidActiveSet || _scheduler.ShouldGenerate(State, localNow));

        /// <summary>
        /// Проверяет, должен ли квест сохранить прогресс после обновления набора.
        /// </summary>
        public bool RetainsProgress(string questId)
        {
            return _retainedQuestIds.Contains(questId);
        }

        /// <summary>
        /// Проверяет получение наград всех квестов текущего Daily-набора.
        /// </summary>
        public bool CanClaimCommonReward(
            IReadOnlyCollection<Quest> dailyQuests)
        {
            if (!IsInitialized ||
                State.CommonRewardClaimed ||
                dailyQuests == null ||
                dailyQuests.Count != DailyQuestCount)
            {
                return false;
            }

            var activeIds = new HashSet<string>(
                State.ActiveQuestIds,
                StringComparer.Ordinal);
            var questIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (Quest quest in dailyQuests)
            {
                if (quest == null ||
                    !quest.IsCompleted ||
                    !quest.IsRewardClaimed ||
                    !questIds.Add(quest.Id))
                {
                    return false;
                }
            }

            return activeIds.SetEquals(questIds);
        }

        /// <summary>
        /// Отмечает общую награду проверенного Daily-набора полученной.
        /// </summary>
        internal void MarkCommonRewardClaimed()
        {
            if (State.PendingCommonRewards.Count > 0)
            {
                State.PendingCommonRewards.RemoveAt(0);
                return;
            }
            State.CommonRewardClaimed = true;
        }

        public DailyQuestService(
            DailyQuestGenerator generator,
            DailyQuestScheduler scheduler)
        {
            _generator = generator ??
                throw new ArgumentNullException(nameof(generator));
            _scheduler = scheduler ??
                throw new ArgumentNullException(nameof(scheduler));
        }

        /// <summary>
        /// Подключает каталог и сохранённое состояние, затем создаёт набор при необходимости.
        /// </summary>
        public bool Init(
            IReadOnlyList<QuestDefinition> dailyPool,
            DailyQuestSetState savedState,
            IReadOnlyCollection<Quest> questStates,
            DateTime localNow,
            DailyCommonRewardDefinition commonReward = null)
        {
            // Подключаем каталог и нормализуем сохранённые данные.
            _dailyPool = dailyPool ??
                throw new ArgumentNullException(nameof(dailyPool));
            _commonReward = commonReward;
            State = savedState ?? new DailyQuestSetState();
            NormalizeState();
            _hasValidActiveSet = HasValidActiveSet();
            IsInitialized = true;
            _nextGenerationTime =
                _scheduler.GetNextGenerationTime(localNow);

            // Создаём первый или пропущенный дневной набор.
            return GenerateIfNeeded(questStates, localNow);
        }

        /// <summary>
        /// Проверяет наступление нового дня и обновляет набор.
        /// </summary>
        public bool Update(
            DateTime localNow,
            IReadOnlyCollection<Quest> questStates)
        {
            if (!IsInitialized)
            {
                return false;
            }

            // До ближайшей полуночи набор остаётся неизменным.
            if (!_scheduler.ShouldGenerate(State, localNow) &&
                _hasValidActiveSet)
            {
                return false;
            }

            // Обновляем набор и рассчитываем следующую проверку.
            bool changed = GenerateIfNeeded(questStates, localNow);
            _nextGenerationTime =
                _scheduler.GetNextGenerationTime(localNow);
            return changed;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Запускает штатную генерацию для следующего дневного периода.
        /// </summary>
        public bool GenerateNextSetForTesting(
            IReadOnlyCollection<Quest> questStates)
        {
            if (!IsInitialized)
            {
                return false;
            }

            return Update(_nextGenerationTime, questStates);
        }
#endif

        private bool GenerateIfNeeded(
            IReadOnlyCollection<Quest> questStates,
            DateTime localNow)
        {
            if (!_scheduler.ShouldGenerate(State, localNow) &&
                _hasValidActiveSet)
            {
                return false;
            }

            // Доступная награда прежнего набора переживает ротацию и сохраняет прежний размер.
            var previousQuests = (questStates ?? Array.Empty<Quest>())
                .Where(quest => quest != null && State.ActiveQuestIds.Contains(quest.QuestId)).ToList();
            if (_hasValidActiveSet && _commonReward != null && CanClaimCommonReward(previousQuests) &&
                !State.PendingCommonRewards.Any(reward => reward.SetId == State.SetId))
            {
                State.PendingCommonRewards.Add(new PendingDailyCommonReward
                {
                    SetId = State.SetId,
                    RewardType = _commonReward.RewardType,
                    RewardAmount = _commonReward.RewardAmount
                });
            }

            // Защищаем завершённые квесты с незабранной наградой.
            var activeIds = new HashSet<string>(
                State.ActiveQuestIds,
                StringComparer.Ordinal);
            var protectedIds = new HashSet<string>(
                (questStates ?? Array.Empty<Quest>())
                .Where(quest =>
                    quest != null &&
                    activeIds.Contains(quest.QuestId) &&
                    quest.CanClaimReward)
                .Select(quest => quest.QuestId),
                StringComparer.Ordinal);
            _retainedQuestIds = protectedIds;

            // Генератор сохраняет защищённые слоты и заполняет свободные.
            IReadOnlyList<string> generatedSet = _generator.Generate(
                _dailyPool,
                State.ActiveQuestIds,
                protectedIds,
                State.LastGeneratedQuestIds);
            List<string> newQuestIds = generatedSet
                .Where(questId => !protectedIds.Contains(questId))
                .ToList();

            State.ActiveQuestIds = generatedSet.ToList();
            State.LastGeneratedQuestIds = generatedSet.ToList();
            State.GenerationDate =
                _scheduler.GetGenerationDate(localNow);
            if (!State.UsedGenerationDates.Contains(State.GenerationDate))
                State.UsedGenerationDates.Add(State.GenerationDate);
            if (newQuestIds.Count > 0)
            {
                State.CommonRewardClaimed = false;
                State.SetId = Guid.NewGuid().ToString("N");
            }

            _hasValidActiveSet = true;

            return true;
        }

        private bool HasValidActiveSet()
        {
            if (State.ActiveQuestIds.Count != DailyQuestCount ||
                State.ActiveQuestIds
                    .Distinct(StringComparer.Ordinal)
                    .Count() != DailyQuestCount)
            {
                return false;
            }

            var knownIds = new HashSet<string>(
                _dailyPool.Select(definition => definition.Id),
                StringComparer.Ordinal);
            return State.ActiveQuestIds.All(knownIds.Contains);
        }

        private void NormalizeState()
        {
            State.GenerationDate ??= string.Empty;
            State.ActiveQuestIds ??= new List<string>();
            State.LastGeneratedQuestIds ??= new List<string>();
            State.UsedGenerationDates ??= new List<string>();
            State.PendingCommonRewards ??= new List<PendingDailyCommonReward>();
            if (string.IsNullOrWhiteSpace(State.SetId)) State.SetId = Guid.NewGuid().ToString("N");
            if (!string.IsNullOrWhiteSpace(State.GenerationDate) &&
                !State.UsedGenerationDates.Contains(State.GenerationDate))
                State.UsedGenerationDates.Add(State.GenerationDate);
            State.PendingCommonRewards.RemoveAll(reward => reward == null ||
                string.IsNullOrWhiteSpace(reward.SetId) || reward.RewardAmount <= 0);
        }
    }
}
