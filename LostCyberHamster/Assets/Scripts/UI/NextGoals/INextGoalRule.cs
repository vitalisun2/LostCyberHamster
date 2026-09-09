using System.Collections.Generic;

namespace LostCyberHamster.UI
{
    /// <summary>Собирает доступные цели одной категории из текущих игровых систем.</summary>
    internal interface INextGoalRule
    {
        NextGoalKind Kind { get; }
        void Collect(List<NextGoalCandidate> candidates, NextGoalConfiguration configuration);
    }
}
