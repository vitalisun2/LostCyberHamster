using System;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Структурированная запись лога бота. Используется BotSessionReport для аналитики.
    /// </summary>
    [Serializable]
    public class BotLogEntry
    {
        public float Timestamp;
        public BotLogEntryType EntryType;
        public string EventName;
        public string Details;

        // Action-specific
        public BotAction Action;
        public string Reason;
        public float Confidence;
        public bool IsPlanned;

        // State snapshot
        public HamsterStateEnum HamsterState;
        public int Energy;
        public int Lives;
        public bool IsOnBottomLine;
        public int UltaCharge;

        /// <summary>
        /// Создаёт запись действия бота.
        /// </summary>
        public static BotLogEntry FromAction(
            float timestamp, BotDecision decision,
            HamsterStateEnum state, int energy, int lives, bool isOnBottom, int ulta)
        {
            return new BotLogEntry
            {
                Timestamp = timestamp,
                EntryType = BotLogEntryType.Action,
                EventName = decision.Action.ToString(),
                Details = decision.Reason,
                Action = decision.Action,
                Reason = decision.Reason,
                Confidence = decision.Confidence,
                IsPlanned = decision.IsPlanned,
                HamsterState = state,
                Energy = energy,
                Lives = lives,
                IsOnBottomLine = isOnBottom,
                UltaCharge = ulta
            };
        }

        /// <summary>
        /// Создаёт запись игрового события.
        /// </summary>
        public static BotLogEntry FromEvent(float timestamp, string eventType, string data)
        {
            return new BotLogEntry
            {
                Timestamp = timestamp,
                EntryType = BotLogEntryType.GameEvent,
                EventName = eventType,
                Details = data
            };
        }

        /// <summary>
        /// Создаёт запись изменения стейта хомяка.
        /// </summary>
        public static BotLogEntry FromStateChange(
            float timestamp, HamsterStateEnum newState, HamsterStateEnum oldState)
        {
            return new BotLogEntry
            {
                Timestamp = timestamp,
                EntryType = BotLogEntryType.StateChange,
                EventName = "StateChange",
                Details = $"{oldState} -> {newState}",
                HamsterState = newState
            };
        }
    }

    public enum BotLogEntryType
    {
        Action,
        GameEvent,
        StateChange,
        Validation
    }
}
