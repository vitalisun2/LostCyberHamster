using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.SwitchLane;

namespace Assets.Scripts.Bot.Strategies.RoofSwitchLane
{
    /// <summary>
    /// Находит безопасное окно запуска roof switch-lane и roof support на целевой линии.
    /// </summary>
    internal sealed class RoofSwitchLaneWindowFinder
    {
        /// <summary>
        /// Рассчитывает безопасные окна запуска смены линии.
        /// </summary>
        private readonly SwitchLaneFireWindowCalculator _fireWindowCalculator;

        public RoofSwitchLaneWindowFinder(SwitchLaneFireWindowCalculator fireWindowCalculator)
        {
            _fireWindowCalculator = fireWindowCalculator;
        }

        /// <summary>
        /// Возвращает окно запуска, если target roof-line доступна до deadline context obstacle.
        /// </summary>
        public bool TryFind(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            RoofSwitchLaneTarget target,
            out RoofSwitchLaneWindow window,
            out string deadEndReason)
        {
            // Проверяет входные данные.
            window = default;
            deadEndReason = null;
            HamsterSnapshot hamster = planningState?.Hamster;
            if (hamster == null
                || worldSnapshot?.Obstacles == null
                || target.ContextObstacle == null)
            {
                return false;
            }

            // Ограничивает запуск deadline-объектом.
            if (!_fireWindowCalculator.TryGetLatestFireShift(
                    hamster,
                    target.ContextObstacle,
                    out float latestFireShift))
            {
                deadEndReason = "Нет безопасного окна для смены линии с крыши: deadline уже пройден.";
                return false;
            }

            // Ограничивает запуск концом текущей roof-chain.
            if (!TryConstrainLatestFireShiftByCurrentRoofRun(
                    planningState,
                    worldSnapshot,
                    latestFireShift,
                    out latestFireShift,
                    out deadEndReason))
            {
                return false;
            }

            // Находит безопасное окно с roof support на целевой линии.
            if (!_fireWindowCalculator.TrySelectRelevantFireWindowSample(
                    worldSnapshot,
                    hamster,
                    target.TargetBottomLine,
                    latestFireShift,
                    out SwitchLaneFireWindowSample fireWindowSample,
                    requireTargetRoofSupport: true))
            {
                deadEndReason = "Нет безопасного окна для смены линии с крыши: целевая roof-line недоступна.";
                return false;
            }

            // Фиксирует roof support для runtime после tap.
            if (!_fireWindowCalculator.TryFindTargetRoofSupportAtFireShift(
                    worldSnapshot,
                    hamster,
                    target.TargetBottomLine,
                    fireWindowSample.FireShift,
                    out ObstacleSnapshot targetRoof)
                || !TryFindObstacleIndex(worldSnapshot, targetRoof, out int targetRoofIndex))
            {
                deadEndReason = "Нет безопасного окна для смены линии с крыши: target roof support не найден.";
                return false;
            }

            // Возвращает выбранное окно.
            window = new RoofSwitchLaneWindow(
                targetRoof,
                targetRoofIndex,
                fireWindowSample);
            return true;
        }

        /// <summary>
        /// Ограничивает запуск текущей passive roof-chain: смещение должно завершиться до RunFromRoof.
        /// </summary>
        private static bool TryConstrainLatestFireShiftByCurrentRoofRun(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            float latestFireShift,
            out float constrainedLatestFireShift,
            out string deadEndReason)
        {
            // Инициализирует результат.
            constrainedLatestFireShift = latestFireShift;
            deadEndReason = null;

            // Проверяет состояние хомяка.
            HamsterSnapshot hamster = planningState?.Hamster;
            if (hamster == null)
                return false;

            // Находит последнюю крышу текущей passive roof-chain.
            if (!RoofRunProjection.TryFindLastPassiveRoof(
                    planningState,
                    worldSnapshot,
                    out ObstacleSnapshot lastRoof,
                    out _))
            {
                deadEndReason = "Нет безопасного окна для смены линии с крыши: текущая roof-chain не найдена.";
                return false;
            }

            // Рассчитывает deadline до схода с текущей roof-chain.
            float roofExitStartShift = lastRoof.RightX
                + hamster.Width * RoofRunProjection.PassiveContinuationGapFactor
                - hamster.HamsterRightX;
            float latestBeforeRoofExit = roofExitStartShift - SwitchLaneTiming.DecisionTravel;
            if (latestBeforeRoofExit <= 0f)
            {
                deadEndReason = "Нет безопасного окна для смены линии с крыши: текущая roof-chain закончится до завершения смещения.";
                return false;
            }

            // Сужает внешний deadline.
            if (latestBeforeRoofExit < constrainedLatestFireShift)
                constrainedLatestFireShift = latestBeforeRoofExit;

            // Возвращает наличие положительного окна.
            return constrainedLatestFireShift > 0f;
        }

        /// <summary>
        /// Находит world-index obstacle по instance id.
        /// </summary>
        private static bool TryFindObstacleIndex(
            WorldSnapshot worldSnapshot,
            ObstacleSnapshot obstacle,
            out int obstacleIndex)
        {
            // Инициализирует результат.
            obstacleIndex = -1;
            if (worldSnapshot?.Obstacles == null || obstacle == null)
                return false;

            // Ищет obstacle по instance id.
            for (int index = 0; index < worldSnapshot.Obstacles.Count; index++)
            {
                if (worldSnapshot.Obstacles[index].InstanceId != obstacle.InstanceId)
                    continue;

                obstacleIndex = index;
                return true;
            }

            return false;
        }
    }
}
