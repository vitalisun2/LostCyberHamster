namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Решение, принятое ботом в текущем кадре.
    /// </summary>
    public struct BotDecision
    {
        /// <summary>Действие, которое нужно выполнить.</summary>
        public BotAction Action;

        /// <summary>Человекочитаемое объяснение для лога.</summary>
        public string Reason;

        /// <summary>0..1 — уверенность бота в решении.</summary>
        public float Confidence;

        /// <summary>Был ли решение принято планировщиком (true) или реактивной системой (false).</summary>
        public bool IsPlanned;

        public static BotDecision DoNothing(string reason = "idle")
        {
            return new BotDecision
            {
                Action = BotAction.None,
                Reason = reason,
                Confidence = 1f,
                IsPlanned = false
            };
        }

        public static BotDecision Urgent(BotAction action, string reason, float confidence = 0.9f)
        {
            return new BotDecision
            {
                Action = action,
                Reason = reason,
                Confidence = confidence,
                IsPlanned = false
            };
        }

        public static BotDecision Planned(BotAction action, string reason, float confidence = 0.8f)
        {
            return new BotDecision
            {
                Action = action,
                Reason = reason,
                Confidence = confidence,
                IsPlanned = true
            };
        }

        public override string ToString()
        {
            var source = IsPlanned ? "PLAN" : "REACT";
            return $"[{source}] {Action} ({Confidence:P0}): {Reason}";
        }
    }
}
