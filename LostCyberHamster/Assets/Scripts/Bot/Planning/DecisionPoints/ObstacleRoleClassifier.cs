using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;

namespace Assets.Scripts.Bot.Planning.DecisionPoints
{
    /// <summary>
    /// Назначает role-based factual-роли obstacle через существующие planning classifiers.
    /// </summary>
    internal static class ObstacleRoleClassifier
    {
        /// <summary>
        /// Возвращает набор ролей obstacle в текущем projected planning-состоянии.
        /// </summary>
        public static HashSet<ObstacleRole> GetRoles(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            ObstacleSnapshot obstacle)
        {
            if (obstacle == null || obstacle.IsRemovedInPlanning)
                return new HashSet<ObstacleRole>();

            var roles = new HashSet<ObstacleRole>();

            // Базовые type facts берутся из единого ObstacleClassifier.
            if (ObstacleClassifier.DamagesOnGroundContact(obstacle.ObstacleType))
                roles.Add(ObstacleRole.BlockingThreat);

            if (ObstacleClassifier.IsObstacleWithRoof(obstacle.ObstacleType))
                roles.Add(ObstacleRole.RoofSupport);

            if (ObstacleClassifier.CanJumpOnGroundObstacle(obstacle.ObstacleType)
                || ObstacleClassifier.CanJumpOnFromRoofObstacle(obstacle.ObstacleType))
            {
                roles.Add(ObstacleRole.Target);
            }

            // RoofOccupantHazard зависит от текущего roof path, поэтому делегируется RoofRunProjection.
            if (RoofRunProjection.TryFindDamagingOccupantOnPassiveRoofPath(
                    planningState,
                    worldSnapshot,
                    obstacle,
                    out _,
                    out _))
            {
                roles.Add(ObstacleRole.RoofOccupantHazard);
            }

            return roles;
        }
    }
}
