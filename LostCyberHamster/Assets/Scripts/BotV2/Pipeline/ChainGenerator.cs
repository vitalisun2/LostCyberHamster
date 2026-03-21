using System.Collections.Generic;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Перечислитель safe ветвей планировщика.
    /// Строит все допустимые ветви до заданной глубины, не выбирает победителя.
    /// Выбор лучшей ветви выполняет BranchEvaluator.
    /// </summary>
    public class ChainGenerator
    {
        private const int MaxBranchDepth = 5;
        private const float ImminentThreatDistance = 4.5f;
        private readonly StateProjector _stateProjector = new StateProjector();

        public List<ChainCandidate> Generate(
            BotSceneSnapshot snapshot,
            List<ChainStep> firstStepCandidates,
            ObjectClassifier classifier,
            ActionGenerator actionGenerator)
        {
            var result = new List<ChainCandidate>();
            if (firstStepCandidates == null || firstStepCandidates.Count == 0)
                return result;

            for (int i = 0; i < firstStepCandidates.Count; i++)
            {
                var first = firstStepCandidates[i];
                if (first == null)
                    continue;

                ExploreBranch(
                    snapshot,
                    classifier,
                    actionGenerator,
                    first,
                    branchStartEnergy: snapshot.Energy,
                    depth: 1,
                    stepsSoFar: new List<ChainStep>(),
                    collectedSoFar: new List<ObstacleInfo>(),
                    originalSnapshot: snapshot,
                    result: result);
            }

            return result;
        }

        private void ExploreBranch(
            BotSceneSnapshot snapshot,
            ObjectClassifier classifier,
            ActionGenerator actionGenerator,
            ChainStep step,
            int branchStartEnergy,
            int depth,
            List<ChainStep> stepsSoFar,
            List<ObstacleInfo> collectedSoFar,
            BotSceneSnapshot originalSnapshot,
            List<ChainCandidate> result)
        {
            stepsSoFar.Add(step);

            var projection = _stateProjector.Project(snapshot, step);
            if (!projection.IsSafe || projection.NextState == null)
                return;

            var branchCollected = new List<ObstacleInfo>(collectedSoFar);
            if (projection.CollectedObjects != null && projection.CollectedObjects.Count > 0)
                branchCollected.AddRange(projection.CollectedObjects);

            var projectedSnapshot = projection.NextState.ToSnapshot();
            classifier.Classify(projectedSnapshot);

            var nextSteps = IsEligibleForNextProjection(projectedSnapshot, step)
                ? FilterNextSteps(actionGenerator.Generate(projectedSnapshot))
                : new List<ChainStep>();
            bool hasImminentThreat = HasImminentSameLaneThreat(projectedSnapshot);
            if (hasImminentThreat)
            {
                nextSteps.RemoveAll(candidate => candidate.Rank != DecisionRank.ThreatSafety);
            }

            // Always add the chain candidate. The projected imminent threat (if any)
            // will be handled by the bot's reactive pipeline after this step completes.
            // AllStepsSafe reflects only the idle-period safety of the first step,
            // not post-step projected threats — this ensures energy-free SwitchLane
            // chains are not penalised relative to energy-costly Jump chains.
            result.Add(BuildCandidate(
                stepsSoFar,
                branchCollected,
                branchStartEnergy,
                projection.NextState.Energy,
                originalSnapshot));

            bool reachedDepthLimit = depth >= MaxBranchDepth;
            if (reachedDepthLimit || nextSteps.Count == 0)
            {
                return;
            }

            for (int i = 0; i < nextSteps.Count; i++)
            {
                var next = nextSteps[i];
                if (next == null)
                    continue;

                ExploreBranch(
                    projectedSnapshot,
                    classifier,
                    actionGenerator,
                    next,
                    branchStartEnergy,
                    depth + 1,
                    new List<ChainStep>(stepsSoFar),
                    new List<ObstacleInfo>(branchCollected),
                    originalSnapshot,
                    result);
            }
        }

        private static ChainCandidate BuildCandidate(
            List<ChainStep> steps,
            List<ObstacleInfo> collectedObjects,
            int startEnergy,
            int endEnergy,
            BotSceneSnapshot originalSnapshot)
        {
            int totalProfit = 0;
            int totalEnergyCost = 0;
            DecisionRank bestRank = DecisionRank.ThreatSafety;
            var targetedCollectibleIds = new HashSet<int>();

            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                totalProfit += step.ProfitScore;
                totalEnergyCost += step.EnergyCost;

                if (i == 0 || step.Rank < bestRank)
                    bestRank = step.Rank;

                if (step.TargetObstacle.Category == ObjectCategory.Collectible)
                    targetedCollectibleIds.Add(step.TargetObstacle.StableId);
            }

            for (int i = 0; i < collectedObjects.Count; i++)
            {
                var collectible = collectedObjects[i];
                if (targetedCollectibleIds.Contains(collectible.StableId))
                    continue;

                totalProfit += GetPassiveCollectibleProfit(collectible.Type);
                DecisionRank collectibleRank = collectible.Type == ObstacleTypeEnum.collectableLife
                    ? DecisionRank.LifeCollectible
                    : DecisionRank.OtherCollectible;

                if (collectibleRank < bestRank)
                    bestRank = collectibleRank;
            }

            return new ChainCandidate
            {
                Steps = new List<ChainStep>(steps),
                Outcome = new BranchOutcome
                {
                    CollectedObjects = new List<ObstacleInfo>(collectedObjects),
                    TotalProfit = totalProfit,
                    TotalEnergyCost = totalEnergyCost,
                    NetEnergyGain = endEnergy - startEnergy,
                    BestRank = bestRank,
                    AllStepsSafe = IsIdlePeriodSafe(originalSnapshot, steps[0])
                }
            };
        }

        /// <summary>
        /// Определяет, стоит ли пытаться проецировать следующий шаг.
        /// SwitchLane всегда допускает продолжение: проекция чисто геометрическая
        /// (смена полосы + advance), не зависит от типа целевого препятствия.
        /// Для Jump/SuperJump крупные road-угрозы (big/medium roofs) по-прежнему
        /// исключены из-за ненадёжности проекции приземления.
        /// </summary>
        private static bool IsEligibleForNextProjection(BotSceneSnapshot snapshot, ChainStep first)
        {
            if (first.TargetObstacle.Category == ObjectCategory.Neutral)
                return false;
            if (!IsChainAction(first.Action))
                return false;

            // SwitchLane projection is purely geometric and always reliable.
            if (first.Action == BotAction.SwitchLane)
                return true;

            if (first.TargetObstacle.Category == ObjectCategory.Threat)
            {
                if (!IsChainableThreat(first.TargetObstacle.Type))
                    return false;
                return IsOnSameLane(snapshot, first.TargetObstacle);
            }

            return true;
        }

        private static List<ChainStep> FilterNextSteps(List<ChainStep> secondCandidates)
        {
            var result = new List<ChainStep>();
            if (secondCandidates == null)
                return result;

            for (int i = 0; i < secondCandidates.Count; i++)
            {
                var second = secondCandidates[i];
                if (second == null)
                    continue;
                if (second.TargetObstacle.Category == ObjectCategory.Neutral)
                    continue;
                if (!IsChainAction(second.Action))
                    continue;

                if (second.TargetObstacle.Category == ObjectCategory.Threat &&
                    !IsChainableThreat(second.TargetObstacle.Type))
                    continue;

                result.Add(second);
            }

            return result;
        }

        private static int GetPassiveCollectibleProfit(ObstacleTypeEnum type)
        {
            switch (type)
            {
                case ObstacleTypeEnum.collectableLife:
                    return 50;
                case ObstacleTypeEnum.collectableCrystal:
                    return 14;
                case ObstacleTypeEnum.collectableEnergetic:
                case ObstacleTypeEnum.collectablePizza:
                    return 10;
                case ObstacleTypeEnum.collectableCoin:
                    return 3;
                default:
                    return 1;
            }
        }

        /// <summary>
        /// Проверяет, переживёт ли хомяк период ожидания перед исполнением первого шага.
        /// Если цель далеко и executeAt мал, мир прокрутится на (target.dist - executeAt) единиц
        /// до момента fire. Любая same-lane threat ближе этого расстояния доедет до хомяка раньше.
        /// </summary>
        private static bool IsIdlePeriodSafe(BotSceneSnapshot snapshot, ChainStep firstStep)
        {
            float waitTravel = firstStep.TargetObstacle.DistanceToHamster - firstStep.ExecuteAtDistance;
            if (waitTravel <= 0f)
                return true;

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obstacle = snapshot.VisibleObjects[i];
                if (obstacle.Category != ObjectCategory.Threat)
                    continue;
                if (!IsOnSameLane(snapshot, obstacle))
                    continue;
                if (obstacle.DistanceToHamster <= 0f)
                    continue;
                if (obstacle.StableId == firstStep.TargetObstacle.StableId)
                    continue;

                if (obstacle.DistanceToHamster < waitTravel)
                    return false;
            }

            return true;
        }

        private static bool HasImminentSameLaneThreat(BotSceneSnapshot snapshot)
        {
            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obstacle = snapshot.VisibleObjects[i];
                if (obstacle.Category != ObjectCategory.Threat)
                    continue;
                if (obstacle.DistanceToHamster < 0f || obstacle.DistanceToHamster > ImminentThreatDistance)
                    continue;
                if (!IsOnSameLane(snapshot, obstacle))
                    continue;

                return true;
            }

            return false;
        }

        private static bool IsOnSameLane(BotSceneSnapshot snapshot, ObstacleInfo obstacle)
        {
            return snapshot.HamsterOnBottom == !obstacle.IsTopLane;
        }

        /// <summary>
        /// Угрозы, которые planner умеет надёжно продолжать в цепочке.
        /// small road остаются базовым случаем; bigAlive включён отдельно,
        /// потому что его SuperJump теперь синхронизирован с реальным исполнением.
        /// </summary>
        private static bool IsChainableThreat(ObstacleTypeEnum type)
        {
            return type == ObstacleTypeEnum.smallNotAliveRoad ||
                   type == ObstacleTypeEnum.smallNotAliveRoadAndRoof ||
                   type == ObstacleTypeEnum.bigAlive;
        }

        private static bool IsChainAction(BotAction action)
        {
            return action == BotAction.Jump ||
                   action == BotAction.SuperJump ||
                   action == BotAction.SwitchLane;
        }
    }
}
