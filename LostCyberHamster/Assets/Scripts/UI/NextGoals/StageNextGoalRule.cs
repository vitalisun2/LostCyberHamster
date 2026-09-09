using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.System;

namespace LostCyberHamster.UI
{
    /// <summary>Предлагает открытое, ещё не начатое время суток или первую часть новой локации.</summary>
    internal sealed class StageNextGoalRule : INextGoalRule
    {
        public NextGoalKind Kind => NextGoalKind.Stage;

        public void Collect(List<NextGoalCandidate> candidates, NextGoalConfiguration configuration)
        {
            var model = LevelSelectionModel.Create();
            foreach (var location in model.Locations)
            {
                if (!location.IsUnlocked) continue;
                foreach (var part in location.Parts)
                {
                    if (!part.IsUnlocked || part.Levels.Count == 0 || part.Levels.Any(level => level.IsCompleted) ||
                        location.Index == 0 && part.Index == 0) continue;
                    var first = part.Levels[0];
                    if (!first.IsUnlocked) continue;
                    candidates.Add(new NextGoalCandidate(Kind, NextGoalAction.Stage, first.Address,
                        NextGoalText.Get("next_goal_stage_available"),
                        $"{NextGoalText.Get(location.Key)} · {NextGoalText.Get(part.Key)}",
                        NextGoalText.Get("first_session_select_level"), ScreenEnum.SelectLevelScreen));
                }
            }
        }
    }
}
