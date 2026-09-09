using System;
using System.Collections.Generic;

namespace Vues.GameCore.Quests
{
    /// <summary>
    /// Хранит действия текущей попытки до успешного завершения уровня.
    /// </summary>
    internal sealed class QuestAttemptBuffer
    {
        private readonly Dictionary<string, int> _actionCounts = new();
        private bool _isActive;

        public bool IsActive => _isActive;
        public string AttemptId { get; private set; } = string.Empty;

        /// <summary>
        /// Начинает новую попытку и очищает действия прошлой попытки.
        /// </summary>
        public void StartAttempt()
        {
            _actionCounts.Clear();
            AttemptId = Guid.NewGuid().ToString("N");
            _isActive = true;
        }

        /// <summary>
        /// Добавляет действие в активную попытку.
        /// </summary>
        public void Add(ActionCounterQuestEvent questEvent)
        {
            if (questEvent == null)
            {
                throw new ArgumentNullException(nameof(questEvent));
            }

            if (!_isActive)
            {
                return;
            }

            _actionCounts.TryGetValue(
                questEvent.ActionId,
                out int currentCount);
            _actionCounts[questEvent.ActionId] =
                currentCount + questEvent.Count;
        }

        /// <summary>
        /// Завершает успешную попытку и возвращает накопленные действия.
        /// </summary>
        public IReadOnlyList<ActionCounterQuestEvent> CompleteAttempt()
        {
            var events = ReadSnapshot();
            DiscardAttempt();
            return events;
        }

        /// <summary>Возвращает неизменяемую копию действий, сохраняя текущую попытку.</summary>
        public IReadOnlyList<ActionCounterQuestEvent> ReadSnapshot()
        {
            if (!_isActive)
            {
                return Array.Empty<ActionCounterQuestEvent>();
            }

            var bufferedEvents =
                new List<ActionCounterQuestEvent>(
                    _actionCounts.Count);
            foreach (KeyValuePair<string, int> action in _actionCounts)
            {
                bufferedEvents.Add(
                    new ActionCounterQuestEvent(
                        action.Key,
                        action.Value));
            }

            return bufferedEvents.AsReadOnly();
        }

        /// <summary>
        /// Завершает попытку без переноса действий в прогресс.
        /// </summary>
        public void DiscardAttempt()
        {
            _actionCounts.Clear();
            _isActive = false;
            AttemptId = string.Empty;
        }
    }
}
