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

        /// <summary>
        /// Начинает новую попытку и очищает действия прошлой попытки.
        /// </summary>
        public void StartAttempt()
        {
            _actionCounts.Clear();
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

            DiscardAttempt();
            return bufferedEvents;
        }

        /// <summary>
        /// Завершает попытку без переноса действий в прогресс.
        /// </summary>
        public void DiscardAttempt()
        {
            _actionCounts.Clear();
            _isActive = false;
        }
    }
}
