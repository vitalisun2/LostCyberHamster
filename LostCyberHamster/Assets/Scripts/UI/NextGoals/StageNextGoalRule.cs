using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.System;
using GameManagement.Progress;

namespace LostCyberHamster.UI
{
    /// <summary>Предлагает новый этап либо добор звёзд после всех побед активной кампании.</summary>
    internal sealed class StageNextGoalRule : INextGoalRule
    {
        public NextGoalKind Kind => NextGoalKind.Stage;

        public void Collect(List<NextGoalCandidate> candidates, NextGoalConfiguration configuration)
        {
            var model = LevelSelectionModel.Create();
            var mastery = GetMasteryGoal();
            if (mastery != null) candidates.Add(mastery);
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

        /// <summary>Выбирает первый открытый недобор по каталогу; общая цель для карточки и JourneyComplete.</summary>
        internal static NextGoalCandidate GetMasteryGoal()
        {
            var levels = LevelManager.SavedProgressOverview.Levels;
            if (levels.Count == 0 || levels.Any(level => !level.IsCompleted)) return null;
            var target = levels.FirstOrDefault(level => level.IsUnlocked && !level.IsMastered);
            if (target == null) return null;

            // И счётчик, и адрес берём из того же текущего каталога и best-прогресса.
            int stars = levels.Sum(level => level.Stars);
            int maximum = levels.Count * LevelProgressEntry.MaxStars;
            return new NextGoalCandidate(NextGoalKind.Stage, NextGoalAction.Stage, target.Address,
                NextGoalText.Get("long_term_stars_progress", stars, maximum),
                NextGoalText.Get("long_term_stars_target", NextGoalText.Get(target.LocationId),
                    NextGoalText.Get(target.PartOfDayId), target.DisplayOrder),
                NextGoalText.Get("long_term_collect_stars"), ScreenEnum.SelectLevelScreen,
                (float)stars / maximum);
        }
    }
}
