using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning
{
    /// <summary>
    /// Проверяет safety-интервал схода с крыши в ground-contact состояниях.
    /// </summary>
    internal static class RoofExitSafety
    {
        /// <summary>
        /// Возвращает true, если на указанной линии нет damaging ground obstacle в интервале RunFromRoof.
        /// </summary>
        public static bool IsSafeDuringRunFromRoof(
            HamsterSnapshot hamster,
            WorldSnapshot worldSnapshot,
            bool targetBottomLine,
            float startShift,
            float completionWorldShift)
        {
            if (hamster == null || worldSnapshot == null || completionWorldShift < startShift)
                return false;

            for (int obstacleIndex = 0; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (obstacle.IsBottomLine != targetBottomLine)
                    continue;

                if (ObstacleClassifier.IsObstacleWithRoof(obstacle.ObstacleType))
                    continue;

                if (!ObstacleClassifier.DamagesOnGroundContact(obstacle.ObstacleType))
                    continue;

                if (OverlapsHamsterDuringShift(hamster, obstacle, startShift, completionWorldShift))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Возвращает true, если obstacle пересекает хомяка в заданном shift-интервале.
        /// </summary>
        private static bool OverlapsHamsterDuringShift(
            HamsterSnapshot hamster,
            ObstacleSnapshot obstacle,
            float startShift,
            float endShift)
        {
            float firstOverlapShift = obstacle.LeftX - hamster.HamsterRightX;
            float lastOverlapShift = obstacle.RightX - hamster.HamsterLeftX;

            return firstOverlapShift <= endShift
                && lastOverlapShift >= startShift;
        }
    }
}
