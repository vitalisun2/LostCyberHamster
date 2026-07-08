using Assets.Scripts.Bot.Diagnostics;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;
using Assets.Scripts.Bot.Strategies.Shared.Execution;
using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using UnityEngine;

namespace Assets.Scripts.Bot.Strategies.PassiveAdvance
{
    /// <summary>
    /// Исполняет passive advance как no-input ожидание ухода boundary obstacle.
    /// </summary>
    internal sealed class PassiveAdvanceExecutor : IActionExecutionHandler
    {
        private const float CompletionEpsilon = 0.01f;

        private readonly LiveObstacleResolver _liveObstacleResolver;
        private readonly SnapshotBuilder _snapshotBuilder = new SnapshotBuilder();

        public PassiveAdvanceExecutor(LiveObstacleResolver liveObstacleResolver)
        {
            _liveObstacleResolver = liveObstacleResolver;
        }

        /// <summary>
        /// Фиксирует начало no-input ожидания.
        /// </summary>
        public ActionFireResult TryFire(Hamster hamster, PlannedAction action)
        {
            if (hamster == null
                || action == null
                || action.Kind != BotActionKind.PassiveAdvance
                || !action.TargetObstacleInstanceId.HasValue)
            {
                return ActionFireResult.Cancelled;
            }

            if (!CanAdvance(hamster, action))
                return ActionFireResult.Cancelled;

            bool hasTargetBounds = TryGetTargetBounds(action, out Bounds targetBounds);
            float obstacleLeftX = hasTargetBounds
                ? targetBounds.min.x
                : action.RenderWorldX;
            string diagnosticExtra = BotDiagnostics.IsEnabled(BotDiagnosticCategory.Execution)
                ? BuildRuntimeSafetyExtra(hamster, action, hasTargetBounds, targetBounds)
                : null;
            HamsterActionLogger.LogFire(
                action,
                obstacleLeftX,
                diagnosticExtra);
            return ActionFireResult.Fired;
        }

        /// <summary>
        /// Завершает ожидание, когда boundary obstacle ушел за левую границу хомяка.
        /// </summary>
        public bool IsCompleted(Hamster hamster, PlannedAction action)
        {
            if (hamster == null || action == null || action.Kind != BotActionKind.PassiveAdvance)
                return false;

            if (!CanAdvance(hamster, action))
                return true;

            if (!TryGetTargetBounds(action, out Bounds targetBounds))
            {
                HamsterActionLogger.LogComplete(action, hamster.HamsterState.Value);
                return true;
            }

            if (targetBounds.max.x <= hamster.LeftX + CompletionEpsilon)
            {
                HamsterActionLogger.LogComplete(action, hamster.HamsterState.Value);
                return true;
            }

            return false;
        }

        private static bool CanAdvance(Hamster hamster, PlannedAction action)
        {
            HamsterStateEnum state = hamster.HamsterState.Value;
            if (state == HamsterStateEnum.Run && !hamster.IsDamaged.Value)
                return true;

            HamsterActionLogger.LogCancel(
                action,
                $"state={state} isDamaged={hamster.IsDamaged.Value}");
            return false;
        }

        private bool TryGetTargetBounds(PlannedAction action, out Bounds bounds)
        {
            bounds = default;
            if (!action.TargetObstacleInstanceId.HasValue)
                return false;

            Obstacle target = _liveObstacleResolver.Find(action.TargetObstacleInstanceId.Value);
            if (target == null)
                return false;

            BoxCollider2D collider = target.GetComponentInChildren<BoxCollider2D>();
            if (collider == null)
                return false;

            bounds = collider.bounds;
            return true;
        }

        private string BuildRuntimeSafetyExtra(
            Hamster hamster,
            PlannedAction action,
            bool hasTargetBounds,
            Bounds targetBounds)
        {
            try
            {
                float completionShift = hasTargetBounds
                    ? targetBounds.max.x - hamster.LeftX + CompletionEpsilon
                    : action.CompletionWorldShift;
                WorldSnapshot snapshot = _snapshotBuilder.Build(hamster);
                if (TryFindRuntimeCurrentLaneThreat(
                        snapshot,
                        completionShift,
                        out ObstacleSnapshot threat,
                        out float unsafeStart,
                        out float unsafeEnd,
                        out bool intersects))
                {
                    string threatKind = intersects
                        ? "runtimeBlockingThreat"
                        : "runtimeNearestThreat";
                    return
                        $"runtimeCompletionShift={completionShift:F2} " +
                        $"{threatKind}={FormatObstacle(threat)} " +
                        $"runtimeUnsafe=[{unsafeStart:F2},{unsafeEnd:F2}] " +
                        $"runtimeIntersects={intersects} ";
                }

                return $"runtimeCompletionShift={completionShift:F2} runtimeThreat=none ";
            }
            catch (global::System.Exception exception)
            {
                return $"runtimeSafetyError={exception.GetType().Name} ";
            }
        }

        private static bool TryFindRuntimeCurrentLaneThreat(
            WorldSnapshot snapshot,
            float completionShift,
            out ObstacleSnapshot threat,
            out float unsafeStart,
            out float unsafeEnd,
            out bool intersects)
        {
            threat = null;
            unsafeStart = 0f;
            unsafeEnd = 0f;
            intersects = false;

            if (snapshot?.Hamster == null || snapshot.Obstacles == null)
                return false;

            HamsterSnapshot hamster = snapshot.Hamster;
            for (int obstacleIndex = 0; obstacleIndex < snapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = snapshot.Obstacles[obstacleIndex];
                if (obstacle == null
                    || obstacle.IsBottomLine != hamster.IsOnBottomLine
                    || !ObstacleClassifier.DamagesOnGroundContact(obstacle.ObstacleType))
                {
                    continue;
                }

                float candidateUnsafeStart = obstacle.LeftX - hamster.HamsterRightX;
                float candidateUnsafeEnd = obstacle.RightX - hamster.HamsterLeftX;
                if (candidateUnsafeEnd <= 0f)
                    continue;

                bool candidateIntersects = candidateUnsafeStart < completionShift;
                threat = obstacle;
                unsafeStart = candidateUnsafeStart;
                unsafeEnd = candidateUnsafeEnd;
                intersects = candidateIntersects;
                return true;
            }

            return false;
        }

        private static string FormatObstacle(ObstacleSnapshot obstacle)
        {
            if (obstacle == null)
                return "none";

            return $"{obstacle.ObstacleType}#{obstacle.InstanceId} " +
                   $"lane={(obstacle.IsBottomLine ? "bottom" : "top")} " +
                   $"x=[{obstacle.LeftX:F2},{obstacle.RightX:F2}]";
        }
    }
}
