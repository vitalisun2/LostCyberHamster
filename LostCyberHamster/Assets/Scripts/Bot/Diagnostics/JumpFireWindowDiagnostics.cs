using System.Collections.Generic;
using System.Globalization;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Bot.Strategies.Shared.Timing;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.System;

namespace Assets.Scripts.Bot.Diagnostics
{
    /// <summary>
    /// Пишет диагностические события fire-window без участия в расчёте результата.
    /// </summary>
    internal sealed class JumpFireWindowDiagnostics
    {
        private readonly string _prefix;

        public JumpFireWindowDiagnostics(string prefix)
        {
            _prefix = prefix;
        }

        public void LogWindow(
            string status,
            HamsterSnapshot hamster,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float actionTravel,
            float firstFireShift,
            float lastFireShift)
        {
            if (_prefix == null)
                return;

            DebugManager.DiagLog(
                $"[{_prefix} {status}] " +
                $"target={targetObstacle.ObstacleType} index={targetObstacleIndex} " +
                $"targetLeft={Format(targetObstacle.LeftX)} targetRight={Format(targetObstacle.RightX)} " +
                $"hamsterLeft={Format(hamster.HamsterLeftX)} hamsterRight={Format(hamster.HamsterRightX)} " +
                $"actionTravel={Format(actionTravel)} first={Format(firstFireShift)} last={Format(lastFireShift)}");
        }

        public void LogExactOutcomeSelection(
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            SafeInterval interval,
            float fireShift)
        {
            if (_prefix == null)
                return;

            DebugManager.DiagLog(
                $"[{_prefix} SELECT] " +
                $"target={targetObstacle.ObstacleType} index={targetObstacleIndex} " +
                $"intervalStart={Format(interval.Start)} intervalEnd={Format(interval.End)} " +
                $"fireShift={Format(fireShift)}");
        }

        public void LogNoExactOutcomeInterval(
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            int intervalCount)
        {
            if (_prefix == null)
                return;

            DebugManager.DiagLog(
                $"[{_prefix} NO_EXACT_INTERVAL] " +
                $"target={targetObstacle.ObstacleType} index={targetObstacleIndex} " +
                $"exactIntervals={intervalCount}");
        }

        public void LogResolvedOutcomeAtSelectedShift(
            JumpOutcomeMatcher matcher,
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            List<JumpObstacleData> shiftedObstacles,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            float fireShift,
            float actionTravel)
        {
            if (_prefix == null)
                return;

            JumpResolveResult result = matcher.ResolveAtShift(
                hamster,
                baseObstacles,
                shiftedObstacles,
                fireShift,
                actionTravel);

            bool directTargetMatch = result.TargetIndex == targetObstacleIndex;
            bool chainOverMatch = !directTargetMatch
                                  && JumpOutcomeMatcher.IsTargetMatch(shiftedObstacles, targetObstacleIndex, result.TargetIndex);

            string resolvedTargetType = "none";
            if (result.TargetIndex >= 0 && result.TargetIndex < shiftedObstacles.Count)
                resolvedTargetType = shiftedObstacles[result.TargetIndex].Type.ToString();

            DebugManager.DiagLog(
                $"[{_prefix} RESOLVE] " +
                $"target={targetObstacle.ObstacleType} index={targetObstacleIndex} " +
                $"fireShift={Format(fireShift)} resolvedState={result.State} resolvedTargetIndex={result.TargetIndex} " +
                $"resolvedTargetType={resolvedTargetType} directTargetMatch={directTargetMatch} chainOverMatch={chainOverMatch}");
        }

        private static string Format(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}