using System;
using System.Collections.Generic;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Генерирует шаги только для одной текущей проблемы.
    /// Использует StrategyTable для диспатча только применимых стратегий.
    /// </summary>
    public class ActionGenerator
    {
        private readonly ProjectedWorld _projectedWorld = new ProjectedWorld();
        private readonly Dictionary<BotAction, IActionStrategy> _strategyByAction;

        // Таблица: (onRoof, obstacleType) → какие стратегии применимы
        private readonly Dictionary<(bool onRoof, ObstacleTypeEnum type), BotAction[]> _strategyTable;

        public ActionGenerator(float superJumpLandingOffset = BotConsts.SuperJumpLandingOffsetFallback)
        {
            // Инициализировать все стратегии
            var strategies = new IActionStrategy[]
            {
                // Существующие (переименованные)
                new SwitchLaneStrategy(),
                new JumpOverStrategy(),
                new SuperJumpOverStrategy(superJumpLandingOffset),

                // Новые (skeleton)
                new JumpOnRoofStrategy(),
                new JumpOnTargetStrategy(),
                new SuperJumpOnRoofStrategy(superJumpLandingOffset),
                new SuperJumpOnTargetStrategy(superJumpLandingOffset),
                new RoofJumpOverStrategy(),
                new RoofJumpDownStrategy(),
                new RoofJumpToRoofStrategy(),
                new RoofJumpOnTargetStrategy(),
                new RoofSuperJumpOverStrategy(),
                new RoofSuperJumpDownStrategy(),
                new RoofSuperJumpToRoofStrategy(),
                new RoofSuperJumpOnTargetStrategy(),
                new RoofSwitchLaneStrategy(),
            };

            _strategyByAction = new Dictionary<BotAction, IActionStrategy>();
            foreach (var strategy in strategies)
                _strategyByAction[strategy.Action] = strategy;

            // Инициализировать таблицу стратегий
            _strategyTable = BuildStrategyTable();
        }

        /// <summary>
        /// Генерирует шаги-кандидаты для заданной проблемы, опрашивая только применимые стратегии.
        /// </summary>
        public List<BranchStep> Generate(
            BotSceneSnapshot snapshot,
            ProblemDescriptor problem,
            string logScope = null)
        {
            var result = new List<BranchStep>();
            if (snapshot == null || problem == null)
                return result;

            // Получить список применимых стратегий из таблицы
            var target = problem.SourceObstacle;
            var tableKey = (snapshot.HamsterOnRoof, target.Type);

            if (!_strategyTable.TryGetValue(tableKey, out var applicableActions))
            {
                // Ни одна стратегия не применима к этой ситуации
                return result;
            }

            // Опросить только применимые стратегии
            var candidates = new List<(BotAction action, bool added, string rejectReason)>();

            foreach (var action in applicableActions)
            {
                if (_strategyByAction.TryGetValue(action, out var strategy))
                {
                    bool success = strategy.TryBuildStep(
                        snapshot, problem, _projectedWorld,
                        out BranchStep step, out string rejectReason);
                    candidates.Add((action, success, rejectReason));
                    if (success)
                        result.Add(step);
                }
            }

            // Залогировать результаты
            if (result.Count > 0)
                LogCandidates(problem.SourceObstacle, candidates, snapshot, logScope);

            return result;
        }

        public StepProjectionResult Project(BotSceneSnapshot snapshot, BranchStep step)
        {
            if (_strategyByAction.TryGetValue(step.Action, out var strategy))
                return strategy.Project(snapshot, step, _projectedWorld);

            return new StepProjectionResult
            {
                IsSafe = false,
                DebugReason = $"No strategy for action {step.Action}"
            };
        }

        private Dictionary<(bool onRoof, ObstacleTypeEnum type), BotAction[]> BuildStrategyTable()
        {
            var table = new Dictionary<(bool onRoof, ObstacleTypeEnum type), BotAction[]>();

            // === ДОРОГА (onRoof = false)
            table[(false, ObstacleTypeEnum.smallNotAliveRoad)] = new[]
            {
                BotAction.Jump,
                BotAction.SwitchLane
            };

            table[(false, ObstacleTypeEnum.smallNotAliveRoadAndRoof)] = new[]
            {
                BotAction.Jump,
                BotAction.SwitchLane
            };

            table[(false, ObstacleTypeEnum.bigAlive)] = new[]
            {
                BotAction.SuperJump,
                BotAction.SwitchLane
            };

            table[(false, ObstacleTypeEnum.bigNotAlive)] = new[]
            {
                BotAction.JumpOnRoof,
                BotAction.SwitchLane
            };

            table[(false, ObstacleTypeEnum.mediumNotAlive)] = new[]
            {
                BotAction.JumpOnRoof,
                BotAction.SwitchLane
            };

            table[(false, ObstacleTypeEnum.smallAlive)] = new[]
            {
                BotAction.Jump,
                BotAction.SuperJump
            };

            // === КРЫША (onRoof = true)
            table[(true, ObstacleTypeEnum.smallNotAliveRoadAndRoof)] = new[]
            {
                BotAction.RoofJumpOver,
                BotAction.RoofSuperJumpOver,
                BotAction.RoofSwitchLane
            };

            table[(true, ObstacleTypeEnum.smallAlive)] = new[]
            {
                BotAction.RoofJumpOnTarget,
                BotAction.RoofSuperJumpOnTarget
            };

            table[(true, ObstacleTypeEnum.bigAlive)] = new[]
            {
                BotAction.RoofJumpOnTarget,
                BotAction.RoofSuperJumpOnTarget
            };

            table[(true, ObstacleTypeEnum.bigNotAlive)] = new[]
            {
                BotAction.RoofJumpToRoof,
                BotAction.RoofSuperJumpToRoof
            };

            table[(true, ObstacleTypeEnum.mediumNotAlive)] = new[]
            {
                BotAction.RoofJumpToRoof,
                BotAction.RoofSuperJumpToRoof
            };

            return table;
        }

        private static void LogCandidates(
            ObstacleInfo obstacle,
            List<(BotAction action, bool added, string rejectReason)> candidates,
            BotSceneSnapshot snapshot,
            string logScope)
        {
            BotLogger.LogActionCandidates(obstacle, candidates, snapshot, logScope);
        }
    }
}
