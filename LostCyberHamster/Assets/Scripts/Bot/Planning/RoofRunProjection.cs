using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Описывает roof-obstacles, которые runtime пройдет как продолжение RoofRun без нового действия.
    /// </summary>
    internal static class RoofRunProjection
    {
        /// <summary>
        /// Задает максимальный gap между roof-obstacles, который runtime проходит как непрерывный RoofRun.
        /// </summary>
        internal const float PassiveContinuationGapFactor = 0.7f;

        /// <summary>
        /// Проверяет, является ли obstacle на текущей линии пассивным продолжением RoofRun.
        /// </summary>
        public static bool IsPassiveRoofContinuation(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot obstacle)
        {
            // Отсекает неполный вход.
            if (planningState == null || projectedWorldSnapshot == null || obstacle == null || obstacle.IsRemovedInPlanning)
                return false;

            // Проверяет roof-состояние хомяка.
            HamsterSnapshot hamster = planningState.Hamster;
            if (hamster == null || !hamster.IsOnRoof || !hamster.RoofSupportInstanceId.HasValue)
                return false;

            // Проверяет roof-тип obstacle.
            if (!ObstacleClassifier.IsObstacleWithRoof(obstacle.ObstacleType))
                return false;

            // Принимает текущую roof support.
            int roofSupportInstanceId = hamster.RoofSupportInstanceId.Value;
            if (obstacle.InstanceId == roofSupportInstanceId)
                return true;

            // Находит текущую roof support.
            ObstacleSnapshot currentSupport = FindRoofSupport(
                projectedWorldSnapshot,
                hamster,
                roofSupportInstanceId);

            // Обрабатывает fallback без найденной support.
            if (currentSupport == null)
                return OverlapsHamster(hamster, obstacle);

            // Обрабатывает roof позади или под хомяком.
            if (obstacle.RightX <= currentSupport.LeftX)
                return OverlapsHamster(hamster, obstacle);

            // Проверяет gap до следующей roof.
            float gap = obstacle.LeftX - currentSupport.RightX;
            if (gap <= 0f)
                return true;

            // Сравнивает gap с runtime passive limit.
            float maxPassiveGap = hamster.Width * PassiveContinuationGapFactor;
            return gap <= maxPassiveGap;
        }

        /// <summary>
        /// Ищет последнюю roof для уже подтвержденного roof-состояния хомяка.
        /// </summary>
        public static bool TryFindLastPassiveRoof(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            out ObstacleSnapshot lastRoof,
            out int lastRoofIndex)
        {
            // Инициализирует пустой результат.
            lastRoof = null;
            lastRoofIndex = -1;

            // Отсекает неполный вход.
            if (planningState == null || projectedWorldSnapshot == null)
                return false;

            // Проверяет roof-состояние хомяка.
            HamsterSnapshot hamster = planningState.Hamster;
            if (hamster == null || !hamster.RoofSupportInstanceId.HasValue)
                return false;

            // Находит текущую roof support.
            if (!TryFindRoofSupport(
                    projectedWorldSnapshot,
                    hamster,
                    hamster.RoofSupportInstanceId.Value,
                    out ObstacleSnapshot currentSupport,
                    out int currentSupportIndex))
            {
                return false;
            }

            // Начинает цепочку с текущей support.
            lastRoof = currentSupport;
            lastRoofIndex = currentSupportIndex;

            // Последовательно расширяет passive roof chain вправо.
            float maxPassiveGap = hamster.Width * PassiveContinuationGapFactor;
            for (int obstacleIndex = currentSupportIndex + 1; obstacleIndex < projectedWorldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot candidate = projectedWorldSnapshot.Obstacles[obstacleIndex];
                if (candidate.IsRemovedInPlanning)
                    continue;

                if (candidate.IsBottomLine != hamster.IsOnBottomLine)
                    continue;

                if (!ObstacleClassifier.IsObstacleWithRoof(candidate.ObstacleType))
                    continue;

                float gap = candidate.LeftX - lastRoof.RightX;
                if (gap > maxPassiveGap)
                    break;

                if (candidate.RightX <= lastRoof.RightX)
                    continue;

                lastRoof = candidate;
                lastRoofIndex = obstacleIndex;
            }

            return true;
        }

        /// <summary>
        /// Ищет passive roof support, с которой пересекается occupant на текущей линии.
        /// </summary>
        public static bool TryFindPassiveRoofSupportForOccupant(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot occupant,
            out ObstacleSnapshot support,
            out int supportIndex)
        {
            // Инициализирует пустой результат.
            support = null;
            supportIndex = -1;

            // Отсекает неполный вход.
            if (planningState == null || projectedWorldSnapshot == null || occupant == null || occupant.IsRemovedInPlanning)
                return false;

            // Проверяет roof-состояние хомяка.
            HamsterSnapshot hamster = planningState.Hamster;
            if (hamster == null || !hamster.IsOnRoof || !hamster.RoofSupportInstanceId.HasValue)
                return false;

            // Находит границы текущего passive roof path.
            if (!TryFindRoofSupport(
                    projectedWorldSnapshot,
                    hamster,
                    hamster.RoofSupportInstanceId.Value,
                    out _,
                    out int currentSupportIndex))
            {
                return false;
            }

            if (!TryFindLastPassiveRoof(
                    planningState,
                    projectedWorldSnapshot,
                    out _,
                    out int lastRoofIndex))
            {
                return false;
            }

            // Ищет roof support occupant'а только внутри текущего passive roof path.
            for (int obstacleIndex = currentSupportIndex; obstacleIndex <= lastRoofIndex; obstacleIndex++)
            {
                ObstacleSnapshot candidate = projectedWorldSnapshot.Obstacles[obstacleIndex];
                if (candidate.IsRemovedInPlanning)
                    continue;

                if (candidate.IsBottomLine != hamster.IsOnBottomLine)
                    continue;

                if (!ObstacleClassifier.IsObstacleWithRoof(candidate.ObstacleType))
                    continue;

                if (!CollisionUtils.IsOverlap(
                        occupant.LeftX,
                        occupant.RightX,
                        candidate.LeftX,
                        candidate.RightX))
                {
                    continue;
                }

                support = candidate;
                supportIndex = obstacleIndex;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Ищет support, если obstacle является опасным occupant'ом на текущем passive roof path.
        /// </summary>
        public static bool TryFindDamagingOccupantOnPassiveRoofPath(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleSnapshot occupant,
            out ObstacleSnapshot support,
            out int supportIndex)
        {
            // Инициализирует пустой результат.
            support = null;
            supportIndex = -1;

            // Проверяет вход и фактические признаки roof occupant.
            if (planningState == null || projectedWorldSnapshot == null || occupant == null || occupant.IsRemovedInPlanning)
                return false;

            HamsterSnapshot hamster = planningState.Hamster;
            if (!IsDamagingRoofOccupantCandidate(hamster, occupant))
                return false;

            // Подтверждает принадлежность occupant текущему passive roof path.
            return TryFindPassiveRoofSupportForOccupant(
                planningState,
                projectedWorldSnapshot,
                occupant,
                out support,
                out supportIndex);
        }

        /// <summary>
        /// Возвращает текущую roof support по instance id или null, если она не найдена.
        /// </summary>
        private static ObstacleSnapshot FindRoofSupport(
            WorldSnapshot projectedWorldSnapshot,
            HamsterSnapshot hamster,
            int roofSupportInstanceId)
        {
            // Делегирует поиск helper-методу с индексом.
            if (TryFindRoofSupport(
                    projectedWorldSnapshot,
                    hamster,
                    roofSupportInstanceId,
                    out ObstacleSnapshot support,
                    out int supportIndex))
            {
                return support;
            }

            return null;
        }

        /// <summary>
        /// Ищет текущую roof support по instance id и возвращает ее index в snapshot.
        /// </summary>
        private static bool TryFindRoofSupport(
            WorldSnapshot projectedWorldSnapshot,
            HamsterSnapshot hamster,
            int roofSupportInstanceId,
            out ObstacleSnapshot support,
            out int supportIndex)
        {
            // Инициализирует пустой результат.
            support = null;
            supportIndex = -1;

            // Ищет roof с нужным instance id на текущей линии.
            for (int obstacleIndex = 0; obstacleIndex < projectedWorldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot candidate = projectedWorldSnapshot.Obstacles[obstacleIndex];
                if (candidate.IsRemovedInPlanning)
                    continue;

                if (candidate.InstanceId != roofSupportInstanceId)
                    continue;

                if (candidate.IsBottomLine != hamster.IsOnBottomLine)
                    continue;

                if (ObstacleClassifier.IsObstacleWithRoof(candidate.ObstacleType))
                {
                    support = candidate;
                    supportIndex = obstacleIndex;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Проверяет фактические признаки опасного occupant'а на roof path без поиска support.
        /// </summary>
        private static bool IsDamagingRoofOccupantCandidate(
            HamsterSnapshot hamster,
            ObstacleSnapshot occupant)
        {
            if (hamster == null || occupant == null || occupant.IsRemovedInPlanning)
                return false;

            return occupant.ObstacleType == ObstacleTypeEnum.smallNotAliveRoadAndRoof
                && occupant.IsBottomLine == hamster.IsOnBottomLine
                && occupant.RightX > hamster.HamsterLeftX;
        }

        /// <summary>
        /// Проверяет пересечение obstacle с текущими границами хомяка.
        /// </summary>
        private static bool OverlapsHamster(HamsterSnapshot hamster, ObstacleSnapshot obstacle)
        {
            return obstacle.RightX > hamster.HamsterLeftX
                && obstacle.LeftX < hamster.HamsterRightX;
        }
    }
}
