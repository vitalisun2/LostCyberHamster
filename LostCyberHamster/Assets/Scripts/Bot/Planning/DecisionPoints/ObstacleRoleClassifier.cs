using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Diagnostics;

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
        public static ObstacleRoleMask GetRoleMask(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            ObstacleSnapshot obstacle)
        {
            RuntimePerformanceDiagnostics.Count(RuntimePerformanceCounter.ObstacleRoleClassifierGetRolesCalls);
            if (obstacle == null || obstacle.IsRemovedInPlanning)
            {
                RuntimePerformanceDiagnostics.Count(RuntimePerformanceCounter.ObstacleRoleClassifierEmptyRoleSets);
                return ObstacleRoleMask.None;
            }

            ObstacleRoleMask roleMask = ObstacleRoleMask.None;

            // Базовые type facts берутся из единого ObstacleClassifier.
            if (ObstacleClassifier.DamagesOnGroundContact(obstacle.ObstacleType))
                roleMask |= ObstacleRoleMask.BlockingThreat;

            if (ObstacleClassifier.IsObstacleWithRoof(obstacle.ObstacleType))
                roleMask |= ObstacleRoleMask.RoofSupport;

            if (ObstacleClassifier.CanJumpOnGroundObstacle(obstacle.ObstacleType)
                || ObstacleClassifier.CanJumpOnFromRoofObstacle(obstacle.ObstacleType))
            {
                roleMask |= ObstacleRoleMask.Target;
            }

            // Collectible входит в graph как факт мира; value считается позже в projected ветке.
            if (ObstacleClassifier.IsCollectible(obstacle.ObstacleType))
                roleMask |= ObstacleRoleMask.Collectible;

            // RoofOccupantHazard зависит от текущего roof path, поэтому делегируется RoofRunProjection.
            if (RoofRunProjection.TryFindDamagingOccupantOnPassiveRoofPath(
                    planningState,
                    worldSnapshot,
                    obstacle,
                    out _,
                    out _))
            {
                roleMask |= ObstacleRoleMask.RoofOccupantHazard;
            }

            int roleCount = CountRoles(roleMask);
            if (roleCount == 0)
            {
                RuntimePerformanceDiagnostics.Count(RuntimePerformanceCounter.ObstacleRoleClassifierEmptyRoleSets);
            }
            else
            {
                RuntimePerformanceDiagnostics.Count(RuntimePerformanceCounter.ObstacleRoleClassifierNonEmptyRoleSets);
                RuntimePerformanceDiagnostics.Count(
                    RuntimePerformanceCounter.ObstacleRoleClassifierAssignedRoles,
                    roleCount);
            }

            return roleMask;
        }

        private static int CountRoles(ObstacleRoleMask roleMask)
        {
            int count = 0;
            if ((roleMask & ObstacleRoleMask.BlockingThreat) != 0)
                count++;
            if ((roleMask & ObstacleRoleMask.RoofSupport) != 0)
                count++;
            if ((roleMask & ObstacleRoleMask.Target) != 0)
                count++;
            if ((roleMask & ObstacleRoleMask.RoofOccupantHazard) != 0)
                count++;
            if ((roleMask & ObstacleRoleMask.Collectible) != 0)
                count++;
            return count;
        }
    }
}
