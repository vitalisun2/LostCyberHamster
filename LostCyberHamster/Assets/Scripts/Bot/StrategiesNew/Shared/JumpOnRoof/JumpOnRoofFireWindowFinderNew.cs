using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPointsNew;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.StrategiesNew.Shared.JumpOnRoof
{
    /// <summary>
    /// Подбирает fire shift для посадки на выбранную role-based крышу.
    /// </summary>
    internal sealed class JumpOnRoofFireWindowFinderNew
    {
        private const int EarliestFireShiftSearchIterations = 10;
        private const float PreRoofObstacleWindowOffsetRatio = 0.2f;

        private readonly IJumpOnRoofPolicy _policy;

        /// <summary>
        /// Создает finder для конкретного jump-on-roof policy.
        /// </summary>
        public JumpOnRoofFireWindowFinderNew(IJumpOnRoofPolicy policy)
        {
            _policy = policy;
        }

        /// <summary>
        /// Подбирает fire shift для выбранного roof support и подтверждает outcome resolver-ом.
        /// </summary>
        public bool TryFindFireShift(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleChainNew chain,
            ObstacleSnapshot targetObstacle,
            int targetObstacleIndex,
            int targetObstacleChainIndex,
            float jumpTravel,
            out JumpOnRoofWindowModel window,
            out float fireShift)
        {
            Guard.ThrowIfNull(
                (planningState, nameof(planningState)),
                (projectedWorldSnapshot, nameof(projectedWorldSnapshot)),
                (chain, nameof(chain)),
                (targetObstacle, nameof(targetObstacle)));

            window = default;
            fireShift = 0f;

            if (!TryGetRoofLandingWindow(
                    planningState.Hamster,
                    chain,
                    targetObstacle,
                    targetObstacleIndex,
                    targetObstacleChainIndex,
                    jumpTravel,
                    out window))
            {
                return false;
            }

            List<JumpObstacleData> baseObstacles = JumpObstacleProjection.BuildBase(projectedWorldSnapshot);
            if (HasRoofHazardOnRoofEntryEdge(
                    planningState.Hamster,
                    baseObstacles,
                    targetObstacleIndex))
            {
                return false;
            }

            if (!TryFindEarliestResolverValidFireShift(
                    planningState.Hamster,
                    baseObstacles,
                    window.FirstFireShift,
                    window.LastFireShift,
                    jumpTravel,
                    targetObstacleIndex,
                    hasPreRoofObstacle: targetObstacleChainIndex > 0,
                    out fireShift,
                    out JumpResolveResult selectedOutcome))
            {
                return false;
            }

            LogSelection(
                planningState,
                chain,
                window,
                jumpTravel,
                fireShift,
                selectedOutcome);

            return true;
        }

        /// <summary>
        /// Проверяет, что retained fire shift всё ещё приводит к посадке на ту же крышу.
        /// </summary>
        internal bool CheckRuntimeOutcomeAtFireShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float fireShift,
            float jumpTravel,
            int targetObstacleIndex)
        {
            if (hamster == null
                || baseObstacles == null
                || fireShift < 0f
                || jumpTravel <= 0f)
            {
                return false;
            }

            if (HasRoofHazardOnRoofEntryEdge(
                    hamster,
                    baseObstacles,
                    targetObstacleIndex))
            {
                return false;
            }

            JumpResolveResult result = ResolveRuntimeOutcomeAtFireShift(
                hamster,
                baseObstacles,
                fireShift,
                jumpTravel);

            return IsExpectedOutcome(result, targetObstacleIndex);
        }

        /// <summary>
        /// Вычисляет допустимое окно fire shift для выбранной roof target.
        /// </summary>
        private static bool TryGetRoofLandingWindow(
            HamsterSnapshot hamster,
            ObstacleChainNew chain,
            ObstacleSnapshot roofObstacle,
            int roofWorldIndex,
            int roofChainIndex,
            float jumpTravel,
            out JumpOnRoofWindowModel window)
        {
            window = default;
            if (hamster == null
                || chain == null
                || roofObstacle == null
                || roofWorldIndex < 0
                || roofChainIndex < 0
                || roofChainIndex >= chain.Count
                || jumpTravel <= 0f)
            {
                return false;
            }

            float firstFireShift = CalculateFirstFireShift(hamster, roofObstacle, jumpTravel);
            float lastFireShift = CalculateLastFireShift(
                hamster,
                chain,
                roofObstacle,
                roofChainIndex,
                jumpTravel);

            float fireWindowBoundaryMargin =
                JumpPlanningConstants.GetEffectiveFireWindowBoundaryMargin();
            firstFireShift += fireWindowBoundaryMargin;
            lastFireShift -= fireWindowBoundaryMargin;

            if (lastFireShift <= 0f || firstFireShift >= lastFireShift)
                return false;

            window = new JumpOnRoofWindowModel(
                roofObstacle,
                roofWorldIndex,
                roofChainIndex,
                firstFireShift,
                lastFireShift);
            return true;
        }

        /// <summary>
        /// Получает runtime outcome для заданного fire shift.
        /// </summary>
        private JumpResolveResult ResolveRuntimeOutcomeAtFireShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float fireShift,
            float jumpTravel)
        {
            _policy.GetResolveInput(
                fireShift,
                jumpTravel,
                out float resolveFireShift,
                out float resolveTravel);

            if (resolveTravel <= 0f)
                return new JumpResolveResult(hamster.HamsterState, -1);

            var obstaclesAtFireShift = new List<JumpObstacleData>(baseObstacles.Count);
            JumpObstacleProjection.BuildShifted(baseObstacles, resolveFireShift, obstaclesAtFireShift);

            JumpResolveContext context = new(
                hamster.IsOnBottomLine,
                hamster.HamsterLeftX,
                hamster.HamsterRightX,
                hamster.CenterX,
                hamster.Width,
                resolveTravel,
                resolveTravel,
                damageBigAliveWithoutYByReach: _policy.DamageBigAliveWithoutYByReach);

            return _policy.Resolve(obstaclesAtFireShift, context);
        }

        /// <summary>
        /// Вычисляет левую границу fire-window по достижимости левого края крыши.
        /// </summary>
        private static float CalculateFirstFireShift(
            HamsterSnapshot hamster,
            ObstacleSnapshot roofObstacle,
            float jumpTravel)
        {
            return Math.Max(0f, roofObstacle.LeftX - jumpTravel - hamster.HamsterRightX);
        }

        /// <summary>
        /// Вычисляет правую границу fire-window по chain contact и roof overshoot.
        /// </summary>
        private static float CalculateLastFireShift(
            HamsterSnapshot hamster,
            ObstacleChainNew chain,
            ObstacleSnapshot roofObstacle,
            int roofChainIndex,
            float jumpTravel)
        {
            float chainLeftEdge = roofObstacle.LeftX;
            for (int chainIndex = 0; chainIndex < roofChainIndex; chainIndex++)
            {
                ObstacleSnapshot obstacle = chain.Elements[chainIndex].Obstacle;
                if (obstacle.LeftX < chainLeftEdge)
                    chainLeftEdge = obstacle.LeftX;
            }

            float latestSafeFireShiftBeforeChainContact = chainLeftEdge - hamster.HamsterRightX;
            float latestSafeFireShiftBeforeRoofOvershoot = roofObstacle.RightX - jumpTravel - hamster.HamsterLeftX;
            return Math.Min(
                latestSafeFireShiftBeforeChainContact,
                latestSafeFireShiftBeforeRoofOvershoot);
        }

        /// <summary>
        /// Проверяет, стоит ли roof hazard на входном edge-сегменте выбранной roof support.
        /// </summary>
        private static bool HasRoofHazardOnRoofEntryEdge(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> obstacles,
            int targetRoofIndex)
        {
            if (hamster == null
                || obstacles == null
                || targetRoofIndex < 0
                || targetRoofIndex >= obstacles.Count
                || hamster.Width <= 0f)
            {
                return false;
            }

            JumpObstacleData targetRoof = obstacles[targetRoofIndex];
            if (!CollisionUtils.IsRoofObstacle(targetRoof.Type))
                return false;

            float entryEdgeLeftX = targetRoof.LeftX;
            float entryEdgeRightX = targetRoof.LeftX + hamster.Width;
            for (int obstacleIndex = 0; obstacleIndex < obstacles.Count; obstacleIndex++)
            {
                if (obstacleIndex == targetRoofIndex)
                    continue;

                JumpObstacleData obstacle = obstacles[obstacleIndex];
                if (obstacle.Type != ObstacleTypeEnum.smallNotAliveRoadAndRoof)
                    continue;

                if (obstacle.IsBottomLine != targetRoof.IsBottomLine)
                    continue;

                bool onTargetRoof = CollisionUtils.IsOverlap(
                    obstacle.LeftX,
                    obstacle.RightX,
                    targetRoof.LeftX,
                    targetRoof.RightX);
                if (!onTargetRoof)
                    continue;

                if (CollisionUtils.IsOverlap(
                        obstacle.LeftX,
                        obstacle.RightX,
                        entryEdgeLeftX,
                        entryEdgeRightX))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Ищет самую раннюю resolver-valid точку внутри аналитического окна.
        /// </summary>
        private bool TryFindEarliestResolverValidFireShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float firstFireShift,
            float lastFireShift,
            float jumpTravel,
            int targetObstacleIndex,
            bool hasPreRoofObstacle,
            out float fireShift,
            out JumpResolveResult selectedOutcome)
        {
            fireShift = firstFireShift;
            selectedOutcome = ResolveRuntimeOutcomeAtFireShift(
                hamster,
                baseObstacles,
                fireShift,
                jumpTravel);
            if (IsExpectedOutcome(selectedOutcome, targetObstacleIndex))
                return TryApplyPreRoofOffset(
                    hamster,
                    baseObstacles,
                    lastFireShift,
                    jumpTravel,
                    targetObstacleIndex,
                    hasPreRoofObstacle,
                    ref fireShift,
                    ref selectedOutcome);

            float rightFireShift = lastFireShift;
            JumpResolveResult rightOutcome = ResolveRuntimeOutcomeAtFireShift(
                hamster,
                baseObstacles,
                rightFireShift,
                jumpTravel);
            if (!IsExpectedOutcome(rightOutcome, targetObstacleIndex))
            {
                fireShift = rightFireShift;
                selectedOutcome = rightOutcome;
                return false;
            }

            float leftFireShift = firstFireShift;
            selectedOutcome = rightOutcome;
            for (int iteration = 0; iteration < EarliestFireShiftSearchIterations; iteration++)
            {
                float candidateFireShift = (leftFireShift + rightFireShift) * 0.5f;
                JumpResolveResult candidateOutcome = ResolveRuntimeOutcomeAtFireShift(
                    hamster,
                    baseObstacles,
                    candidateFireShift,
                    jumpTravel);

                if (IsExpectedOutcome(candidateOutcome, targetObstacleIndex))
                {
                    rightFireShift = candidateFireShift;
                    selectedOutcome = candidateOutcome;
                    continue;
                }

                leftFireShift = candidateFireShift;
            }

            fireShift = rightFireShift;
            return TryApplyPreRoofOffset(
                hamster,
                baseObstacles,
                lastFireShift,
                jumpTravel,
                targetObstacleIndex,
                hasPreRoofObstacle,
                ref fireShift,
                ref selectedOutcome);
        }

        /// <summary>
        /// Смещает выбранную точку внутрь окна, если перед крышей есть obstacle.
        /// </summary>
        private bool TryApplyPreRoofOffset(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            float lastFireShift,
            float jumpTravel,
            int targetObstacleIndex,
            bool hasPreRoofObstacle,
            ref float fireShift,
            ref JumpResolveResult selectedOutcome)
        {
            if (!hasPreRoofObstacle)
                return true;

            float preferredFireShift =
                fireShift
                + (lastFireShift - fireShift) * PreRoofObstacleWindowOffsetRatio;
            if (preferredFireShift <= fireShift)
                return true;

            JumpResolveResult preferredOutcome = ResolveRuntimeOutcomeAtFireShift(
                hamster,
                baseObstacles,
                preferredFireShift,
                jumpTravel);
            if (!IsExpectedOutcome(preferredOutcome, targetObstacleIndex))
                return true;

            fireShift = preferredFireShift;
            selectedOutcome = preferredOutcome;
            return true;
        }

        /// <summary>
        /// Проверяет соответствие runtime outcome выбранной roof target.
        /// </summary>
        private bool IsExpectedOutcome(
            JumpResolveResult outcome,
            int targetObstacleIndex)
        {
            return outcome.State == _policy.ExpectedRoofState
                   && outcome.TargetIndex == targetObstacleIndex;
        }

        /// <summary>
        /// Пишет диагностику выбранного окна jump-on-roof.
        /// </summary>
        private void LogSelection(
            PlanningState planningState,
            ObstacleChainNew chain,
            JumpOnRoofWindowModel window,
            float jumpTravel,
            float fireShift,
            JumpResolveResult selectedOutcome)
        {
            ObstacleSnapshot triggerObstacle = chain.FirstObstacle;
            float projectedTriggerX = triggerObstacle.LeftX - fireShift;
            float triggerX = projectedTriggerX + planningState.ProjectionWorldShift;
            float fireWindowBoundaryMargin =
                JumpPlanningConstants.GetEffectiveFireWindowBoundaryMargin();

            DebugManager.DiagLogVerbose(
                $"[{_policy.LogTag} WINDOW] target={window.TargetObstacle.ObstacleType} " +
                $"targetIndex={window.TargetObstacleIndex} roofChainIndex={window.TargetObstacleChainIndex} " +
                $"travel={jumpTravel:F3} " +
                $"first={window.FirstFireShift:F3} last={window.LastFireShift:F3} selected={fireShift:F3} " +
                $"boundaryMargin={fireWindowBoundaryMargin:F3} " +
                $"triggerX={triggerX:F3} triggerLeft={triggerObstacle.LeftX:F3} " +
                $"targetLeft={window.TargetObstacle.LeftX:F3} targetRight={window.TargetObstacle.RightX:F3} " +
                $"projection={planningState.ProjectionWorldShift:F3} " +
                $"outcome={selectedOutcome.State} outcomeTargetIndex={selectedOutcome.TargetIndex}");
        }
    }
}
