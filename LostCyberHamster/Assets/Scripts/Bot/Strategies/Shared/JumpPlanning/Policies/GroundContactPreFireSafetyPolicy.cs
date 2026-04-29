using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.Policies
{
    /// <summary>
    /// Отсекает fire shift, до которого хомяк не может безопасно добежать по земле на текущей линии.
    /// </summary>
    internal sealed class GroundContactPreFireSafetyPolicy : IPreFireSafetyPolicy
    {
        private const float GroundContactSafetyMargin = 0.1f;

        public bool CanWaitUntilFire(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> obstacles,
            float fireShift)
        {
            if (hamster == null || obstacles == null)
                return false;

            for (int obstacleIndex = 0; obstacleIndex < obstacles.Count; obstacleIndex++)
            {
                JumpObstacleData obstacle = obstacles[obstacleIndex];
                if (!IsGroundThreatOnHamsterLane(hamster, obstacle))
                    continue;

                if (HitsHamsterBeforeFire(hamster, obstacle, fireShift))
                    return false;
            }

            return true;
        }

        private static bool IsGroundThreatOnHamsterLane(HamsterSnapshot hamster, JumpObstacleData obstacle)
        {
            return obstacle.IsBottomLine == hamster.IsOnBottomLine
                   && ObstacleClassifier.DamagesOnGroundContact(obstacle.Type);
        }

        private static bool HitsHamsterBeforeFire(HamsterSnapshot hamster, JumpObstacleData obstacle, float fireShift)
        {
            if (obstacle.RightX <= hamster.HamsterLeftX)
                return false;

            float obstacleLeftAtFire = obstacle.LeftX - fireShift;
            return obstacleLeftAtFire < hamster.HamsterRightX + GroundContactSafetyMargin;
        }
    }
}