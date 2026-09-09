using System;

namespace LostCyberHamster.UI
{
    /// <summary>Параметры одного известного правила в локальном JSON.</summary>
    [Serializable]
    public sealed class NextGoalRuleSettings
    {
        public string kind;
        public bool enabled;
        public int priority;
        public string[] screens;
    }
}
