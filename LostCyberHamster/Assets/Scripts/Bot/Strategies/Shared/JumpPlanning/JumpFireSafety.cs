using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning
{
    /// <summary>
    /// Общие проверки безопасности ожидания перед ground-jump action.
    /// </summary>
    internal static class JumpFireSafety
    {
        private const float _groundContactSafetyMargin = 0.1f;

        /// <summary>
        /// Возвращает true, если хомяк может дождаться fire shift без ground contact damage.
        /// </summary>
        public static bool CanWaitUntilFire(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> obstacles,
            float fireShift)
        {
            if (hamster == null || obstacles == null)
                return false;

            for (int obstacleIndex = 0; obstacleIndex < obstacles.Count; obstacleIndex++)
            {
                JumpObstacleData obstacle = obstacles[obstacleIndex];
                if (obstacle.IsBottomLine != hamster.IsOnBottomLine)
                    continue;

                if (!ObstacleClassifier.DamagesOnGroundContact(obstacle.Type))
                    continue;

                if (HitsHamsterBeforeFire(hamster, obstacle, fireShift))
                    return false;
            }

            return true;
        }

        private static bool HitsHamsterBeforeFire(
            HamsterSnapshot hamster,
            JumpObstacleData obstacle,
            float fireShift)
        {
            if (obstacle.RightX <= hamster.HamsterLeftX)
                return false;

            float obstacleLeftAtFire = obstacle.LeftX - fireShift;
            return obstacleLeftAtFire < hamster.HamsterRightX + _groundContactSafetyMargin;
        }
    }
}
