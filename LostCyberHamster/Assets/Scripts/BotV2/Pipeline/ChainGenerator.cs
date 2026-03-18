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

            bool reachedDepthLimit = depth >= MaxBranchDepth;
            if (reachedDepthLimit || nextSteps.Count == 0)
            {
                if (!hasImminentThreat)
                {
                    result.Add(BuildCandidate(
                        stepsSoFar,
                        branchCollected,
                        branchStartEnergy,
                        projection.NextState.Energy));
                }

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
                    result);
            }
        }

        private static ChainCandidate BuildCandidate(
            List<ChainStep> steps,
            List<ObstacleInfo> collectedObjects,
            int startEnergy,
            int endEnergy)
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
                    AllStepsSafe = true
                }
            };
        }

        /// <summary>
        /// Определяет, стоит ли пытаться проецировать второй шаг.
        /// Нейтральные объекты и неподдерживаемые действия не нуждаются в проекции.
        /// Крупные угрозы (big/medium) исключены: их проекция после прыжка слишком ненадёжна.
        /// </summary>
        private static bool IsEligibleForNextProjection(BotSceneSnapshot snapshot, ChainStep first)
        {
            if (first.TargetObstacle.Category == ObjectCategory.Neutral)
                return false;
            if (!IsChainAction(first.Action))
                return false;

            if (first.TargetObstacle.Category == ObjectCategory.Threat)
            {
                if (!IsSmallRoadThreat(first.TargetObstacle.Type))
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
                    !IsSmallRoadThreat(second.TargetObstacle.Type))
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
        /// Малые дорожные угрозы, которые можно перепрыгнуть или обогнуть в цепочке.
        /// Крупные угрозы (big/medium) исключены: их проекция после прыжка слишком ненадёжна.
        /// </summary>
        private static bool IsSmallRoadThreat(ObstacleTypeEnum type)
        {
            return type == ObstacleTypeEnum.smallNotAliveRoad ||
                   type == ObstacleTypeEnum.smallNotAliveRoadAndRoof;
        }

        private static bool IsChainAction(BotAction action)
        {
            return action == BotAction.Jump ||
                   action == BotAction.SuperJump ||
                   action == BotAction.SwitchLane;
        }
    }
}