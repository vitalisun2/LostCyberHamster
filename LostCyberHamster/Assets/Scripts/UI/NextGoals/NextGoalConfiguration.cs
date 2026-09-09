using System;
using System.Collections.Generic;
using System.Linq;

namespace LostCyberHamster.UI
{
    /// <summary>Минимальная конфигурация показа; экономика и доступность принадлежат игровым сервисам.</summary>
    [Serializable]
    public sealed class NextGoalConfiguration
    {
        public int schemaVersion;
        public int configVersion;
        public bool enabled;
        public float questNearCompletionRatio;
        public float repeatAfterSeconds;
        public NextGoalRuleSettings[] rules;

        internal void Validate()
        {
            // Проверяем документ целиком до передачи координатору.
            if (schemaVersion != 1 || configVersion < 1 || rules == null || rules.Length != 4 ||
                float.IsNaN(questNearCompletionRatio) || questNearCompletionRatio <= 0 || questNearCompletionRatio > 1 ||
                float.IsNaN(repeatAfterSeconds) || float.IsInfinity(repeatAfterSeconds) || repeatAfterSeconds < 0)
                throw new InvalidOperationException("Invalid next-goal configuration.");
            var kinds = new HashSet<NextGoalKind>();
            foreach (var rule in rules)
            {
                if (rule == null || !Enum.TryParse(rule.kind, out NextGoalKind kind) ||
                    rule.kind != kind.ToString() || !Enum.IsDefined(typeof(NextGoalKind), kind) || !kinds.Add(kind) || rule.screens == null ||
                    rule.screens.Any(screen => screen != "Home" && screen != "Win" && screen != "SelectLevel"))
                    throw new InvalidOperationException("Invalid next-goal rule.");
            }
        }

        internal NextGoalRuleSettings Find(NextGoalKind kind) => rules.First(rule => rule.kind == kind.ToString());
    }
}
