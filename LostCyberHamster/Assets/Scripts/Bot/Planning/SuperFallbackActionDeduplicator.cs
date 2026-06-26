using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Diagnostics;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.Contracts;

namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Нормализует candidate actions внутри одного прохода <see cref="ActionGenerator.Generate"/>,
    /// убирая redundant super-action, если matching ordinary action уже покрывает тот же obstacle target.
    /// Super-action здесь трактуется как fallback strategy: она нужна только тогда, когда ordinary
    /// strategy не смогла создать безопасный action для той же цели.
    /// </summary>
    internal sealed class SuperFallbackActionDeduplicator
    {
        private static readonly SuperFallbackActionPair[] SuperFallbackActionPairs =
        {
            new SuperFallbackActionPair(BotActionKind.JumpOver, BotActionKind.SuperJumpOver),
            new SuperFallbackActionPair(BotActionKind.JumpOn, BotActionKind.SuperJumpOn),
            new SuperFallbackActionPair(BotActionKind.JumpOnRoof, BotActionKind.SuperJumpOnRoof),
            new SuperFallbackActionPair(BotActionKind.RoofJumpOver, BotActionKind.SuperRoofJumpOver),
            new SuperFallbackActionPair(BotActionKind.JumpFromRoof, BotActionKind.SuperJumpFromRoof),
            new SuperFallbackActionPair(BotActionKind.JumpOnFromRoof, BotActionKind.SuperJumpOnFromRoof),
            new SuperFallbackActionPair(BotActionKind.JumpFromRoofOnRoof, BotActionKind.SuperJumpFromRoofOnRoof)
        };

        private readonly HashSet<ActionFallbackKey> _ordinaryFallbackTargets = new HashSet<ActionFallbackKey>();

        /// <summary>
        /// Очищает накопленные ordinary targets перед генерацией candidates для нового planning state.
        /// Это обязательно при использовании deduplicator как поля <see cref="ActionGenerator"/>,
        /// чтобы ordinary actions из прошлого planning state не влияли на следующий вызов.
        /// </summary>
        public void Reset()
        {
            _ordinaryFallbackTargets.Clear();
        }

        /// <summary>
        /// Принимает ordinary actions и самостоятельные actions, но отклоняет super fallback,
        /// если ранее в этом же generation pass уже был принят matching ordinary action для того же target.
        /// </summary>
        public bool TryAccept(PlannedAction action)
        {
            if (action == null)
                return false;

            if (IsSuperFallbackCoveredByOrdinaryAction(action))
                return false;

            TrackOrdinaryFallbackTarget(action);
            return true;
        }

        /// <summary>
        /// Проверяет контракт порядка strategies, от которого зависит one-pass дедупликация:
        /// каждая ordinary strategy должна быть зарегистрирована раньше matching super strategy.
        /// </summary>
        public static bool IsStrategyOrderValid(IReadOnlyList<IPlanningStrategy> strategies)
        {
            for (int pairIndex = 0; pairIndex < SuperFallbackActionPairs.Length; pairIndex++)
            {
                SuperFallbackActionPair pair = SuperFallbackActionPairs[pairIndex];
                if (!IsStrategyBefore(strategies, pair.OrdinaryActionKind, pair.SuperActionKind))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Формирует diagnostic summary по индексам ordinary/super strategy pairs,
        /// чтобы assertion мог сразу показать, какая fallback-пара нарушила порядок регистрации.
        /// </summary>
        public static string BuildStrategyOrderDiagnostic(IReadOnlyList<IPlanningStrategy> strategies)
        {
            var parts = new List<string>(SuperFallbackActionPairs.Length);
            for (int pairIndex = 0; pairIndex < SuperFallbackActionPairs.Length; pairIndex++)
            {
                SuperFallbackActionPair pair = SuperFallbackActionPairs[pairIndex];
                parts.Add(
                    $"{pair.OrdinaryActionKind}={FindStrategyIndex(strategies, pair.OrdinaryActionKind)}:" +
                    $"{pair.SuperActionKind}={FindStrategyIndex(strategies, pair.SuperActionKind)}");
            }

            return string.Join(" ", parts);
        }

        private bool IsSuperFallbackCoveredByOrdinaryAction(PlannedAction action)
        {
            if (!TryGetCoveredOrdinaryActionKind(action.Kind, out BotActionKind ordinaryActionKind)
                || !action.TargetObstacleInstanceId.HasValue)
            {
                return false;
            }

            var fallbackKey = new ActionFallbackKey(
                ordinaryActionKind,
                action.TargetObstacleInstanceId.Value);
            if (!_ordinaryFallbackTargets.Contains(fallbackKey))
                return false;

            LogSkippedSuperFallback(action);
            return true;
        }

        private void TrackOrdinaryFallbackTarget(PlannedAction action)
        {
            if (!IsOrdinaryFallbackAction(action.Kind)
                || !action.TargetObstacleInstanceId.HasValue)
            {
                return;
            }

            _ordinaryFallbackTargets.Add(new ActionFallbackKey(
                action.Kind,
                action.TargetObstacleInstanceId.Value));
        }

        private static bool IsOrdinaryFallbackAction(BotActionKind actionKind)
        {
            for (int pairIndex = 0; pairIndex < SuperFallbackActionPairs.Length; pairIndex++)
            {
                if (SuperFallbackActionPairs[pairIndex].OrdinaryActionKind == actionKind)
                    return true;
            }

            return false;
        }

        private static bool TryGetCoveredOrdinaryActionKind(
            BotActionKind actionKind,
            out BotActionKind ordinaryActionKind)
        {
            for (int pairIndex = 0; pairIndex < SuperFallbackActionPairs.Length; pairIndex++)
            {
                SuperFallbackActionPair pair = SuperFallbackActionPairs[pairIndex];
                if (pair.SuperActionKind != actionKind)
                    continue;

                ordinaryActionKind = pair.OrdinaryActionKind;
                return true;
            }

            ordinaryActionKind = default;
            return false;
        }

        private static bool IsStrategyBefore(
            IReadOnlyList<IPlanningStrategy> strategies,
            BotActionKind ordinaryActionKind,
            BotActionKind superActionKind)
        {
            int ordinaryIndex = FindStrategyIndex(strategies, ordinaryActionKind);
            int superIndex = FindStrategyIndex(strategies, superActionKind);
            return ordinaryIndex >= 0 && superIndex >= 0 && ordinaryIndex < superIndex;
        }

        private static int FindStrategyIndex(
            IReadOnlyList<IPlanningStrategy> strategies,
            BotActionKind actionKind)
        {
            if (strategies == null)
                return -1;

            for (int strategyIndex = 0; strategyIndex < strategies.Count; strategyIndex++)
            {
                IPlanningStrategy strategy = strategies[strategyIndex];
                if (strategy != null && strategy.ActionKind == actionKind)
                    return strategyIndex;
            }

            return -1;
        }

        private static void LogSkippedSuperFallback(PlannedAction action)
        {
            if (!BotDiagnostics.IsEnabled(BotDiagnosticCategory.Strategy, BotDiagnosticLevel.Verbose))
                return;

            BotDiagnostics.Log(
                BotDiagnosticCategory.Strategy,
                BotDiagnosticLevel.Verbose,
                "[Bot SUPER_FALLBACK] skip=true " +
                $"super={action.Kind} target={FormatNullable(action.TargetObstacleInstanceId)} " +
                $"trigger={FormatNullable(action.TriggerObstacleInstanceId)} " +
                $"desc={action.Description}");
        }

        private static string FormatNullable(int? value)
        {
            return value.HasValue ? value.Value.ToString() : "none";
        }

        private readonly struct ActionFallbackKey : IEquatable<ActionFallbackKey>
        {
            /// <summary>
            /// Создает ключ ordinary action kind + obstacle target, который покрывает matching super fallback.
            /// </summary>
            public ActionFallbackKey(BotActionKind ordinaryActionKind, int targetObstacleInstanceId)
            {
                OrdinaryActionKind = ordinaryActionKind;
                TargetObstacleInstanceId = targetObstacleInstanceId;
            }

            private BotActionKind OrdinaryActionKind { get; }
            private int TargetObstacleInstanceId { get; }

            public bool Equals(ActionFallbackKey other)
            {
                return OrdinaryActionKind == other.OrdinaryActionKind
                    && TargetObstacleInstanceId == other.TargetObstacleInstanceId;
            }

            public override bool Equals(object obj)
            {
                return obj is ActionFallbackKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((int)OrdinaryActionKind * 397) ^ TargetObstacleInstanceId;
                }
            }
        }

        private readonly struct SuperFallbackActionPair
        {
            /// <summary>
            /// Описывает пару, где super action является fallback-версией ordinary action.
            /// </summary>
            public SuperFallbackActionPair(BotActionKind ordinaryActionKind, BotActionKind superActionKind)
            {
                OrdinaryActionKind = ordinaryActionKind;
                SuperActionKind = superActionKind;
            }

            public BotActionKind OrdinaryActionKind { get; }
            public BotActionKind SuperActionKind { get; }
        }
    }
}
