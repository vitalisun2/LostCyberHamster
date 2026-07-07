using System;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.PassiveRoofExit;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Bot.Strategies.SwitchLane;

namespace Assets.Scripts.Bot.Strategies.RoofSwitchLane
{
    /// <summary>
    /// Находит безопасное окно запуска roof switch-lane и тип посадки на целевой линии.
    /// </summary>
    internal sealed class RoofSwitchLaneWindowFinder
    {
        /// <summary>
        /// Рассчитывает безопасные окна запуска смены линии.
        /// </summary>
        private readonly SwitchLaneFireWindowCalculator _fireWindowCalculator;
        private readonly PassiveRoofExitPolicy _passiveRoofExitPolicy;

        public RoofSwitchLaneWindowFinder(SwitchLaneFireWindowCalculator fireWindowCalculator)
        {
            _fireWindowCalculator = fireWindowCalculator;
            _passiveRoofExitPolicy = new PassiveRoofExitPolicy();
        }

        /// <summary>
        /// Возвращает окно запуска, если целевая линия доступна как roof support или безопасная дорога.
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

            // Ограничивает старт tap концом текущей roof-chain.
            if (!TryConstrainLatestFireShiftByCurrentRoofExit(
                    planningState,
                    worldSnapshot,
                    latestFireShift,
                    out latestFireShift,
                    out deadEndReason))
            {
                return false;
            }

            // Сначала сохраняет существующий roof-to-roof сценарий.
            if (TryFindRoofLandingWindow(
                    worldSnapshot,
                    hamster,
                    target.TargetBottomLine,
                    latestFireShift,
                    out window))
            {
                return true;
            }

            // Если крыши нет, пробует безопасную посадку на дорогу.
            if (TryFindRoadLandingWindow(
                    worldSnapshot,
                    hamster,
                    target.TargetBottomLine,
                    latestFireShift,
                    out window,
                    out string roadLandingDeadEndReason))
            {
                return true;
            }

            // Возвращает общую причину недоступности target lane.
            deadEndReason = string.IsNullOrEmpty(roadLandingDeadEndReason)
                ? "Нет безопасного окна для смены линии с крыши: целевая линия недоступна ни как крыша, ни как дорога."
                : roadLandingDeadEndReason;
            return false;
        }

        /// <summary>
        /// Ищет окно смены линии с посадкой на roof support.
        /// </summary>
        private bool TryFindRoofLandingWindow(
            WorldSnapshot worldSnapshot,
            HamsterSnapshot hamster,
            bool targetBottomLine,
            float latestFireShift,
            out RoofSwitchLaneWindow window)
        {
            // Инициализирует результат.
            window = default;

            // Находит безопасное окно с roof support на целевой линии.
            if (!_fireWindowCalculator.TrySelectRelevantFireWindowSample(
                    worldSnapshot,
                    hamster,
                    targetBottomLine,
                    latestFireShift,
                    out SwitchLaneFireWindowSample fireWindowSample,
                    requireTargetRoofSupport: true))
            {
                return false;
            }

            // Фиксирует roof support для runtime после tap.
            if (!_fireWindowCalculator.TryFindTargetRoofSupportAtFireShift(
                    worldSnapshot,
                    hamster,
                    targetBottomLine,
                    fireWindowSample.FireShift,
                    out ObstacleSnapshot targetRoof)
                || !TryFindObstacleIndex(worldSnapshot, targetRoof, out int targetRoofIndex))
            {
                return false;
            }

            // Возвращает roof landing.
            window = new RoofSwitchLaneWindow(
                targetRoof,
                targetRoofIndex,
                fireWindowSample,
                SwitchLaneTiming.DecisionTravel);
            return true;
        }

        /// <summary>
        /// Ищет окно смены линии с посадкой на безопасную дорогу.
        /// </summary>
        private bool TryFindRoadLandingWindow(
            WorldSnapshot worldSnapshot,
            HamsterSnapshot hamster,
            bool targetBottomLine,
            float latestFireShift,
            out RoofSwitchLaneWindow window,
            out string deadEndReason)
        {
            // Инициализирует результат.
            window = default;
            deadEndReason = null;

            // Находит безопасное окно без требования roof support.
            if (!_fireWindowCalculator.TrySelectRelevantFireWindowSample(
                    worldSnapshot,
                    hamster,
                    targetBottomLine,
                    latestFireShift,
                    out SwitchLaneFireWindowSample fireWindowSample,
                    requireTargetRoofSupport: false))
            {
                return false;
            }

            if (!_passiveRoofExitPolicy.TryGetRunFromRoofTravel(out float runFromRoofTravel))
                return false;

            float runFromRoofStartShift = fireWindowSample.FireShift;
            float runFromRoofCompletionShift = runFromRoofStartShift + runFromRoofTravel;
            if (!RoofExitSafety.IsSafeDuringRunFromRoof(
                    hamster,
                    worldSnapshot,
                    targetBottomLine,
                    runFromRoofStartShift,
                    runFromRoofCompletionShift,
                    out deadEndReason))
            {
                return false;
            }

            float postFireWorldShift = Math.Max(
                SwitchLaneTiming.DecisionTravel,
                runFromRoofTravel);

            // Возвращает road landing.
            window = new RoofSwitchLaneWindow(
                targetRoof: null,
                targetRoofIndex: -1,
                fireWindowSample,
                postFireWorldShift);
            return true;
        }

        /// <summary>
        /// Ограничивает запуск текущей passive roof-chain: tap должен начаться до RunFromRoof.
        /// </summary>
        private static bool TryConstrainLatestFireShiftByCurrentRoofExit(
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

            // Рассчитывает deadline старта tap до схода с текущей roof-chain.
            float latestBeforeRoofExit = lastRoof.RightX
                + Assets.Scripts.Consts.GetRoofRunPassiveContinuationGap(hamster.Width)
                - hamster.HamsterRightX;
            if (latestBeforeRoofExit <= 0f)
            {
                deadEndReason = "Нет безопасного окна для смены линии с крыши: текущая roof-chain закончилась до запуска смены линии.";
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
