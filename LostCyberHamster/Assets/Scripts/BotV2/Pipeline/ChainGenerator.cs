using System.Collections.Generic;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Stage 9: генерация двухшаговых цепочек с учётом обеих линий.
    /// Использует one-step ActionGenerator поверх проецированного состояния
    /// и выбирает валидный второй шаг, включая межлинейный переход (SwitchLane).
    /// </summary>
    public class ChainGenerator
    {
        private const float JumpLandingTravel = 3.8f;
        private const float SuperJumpLandingTravel = 4.6f;
        private const float LandingPostFactor = 0.4f;
        private const float PassedObstacleMargin = 0.4f;
        private const float ImminentThreatDistance = 4.5f;

        public List<ChainCandidate> Generate(
            BotSceneSnapshot snapshot,
            List<ChainStep> firstStepCandidates,
            ObjectClassifier classifier,
            ActionGenerator actionGenerator,
            ActionSelector actionSelector)
        {
            var result = new List<ChainCandidate>();
            if (firstStepCandidates == null || firstStepCandidates.Count == 0)
                return result;

            for (int i = 0; i < firstStepCandidates.Count; i++)
            {
                var first = firstStepCandidates[i];
                if (!IsFirstStepEligible(snapshot, first))
                    continue;

                var projected = ProjectAfterFirstStep(snapshot, first);
                var projectedSnapshot = BuildProjectedSnapshot(projected);
                classifier.Classify(projectedSnapshot);

                var secondCandidates = actionGenerator.Generate(projectedSnapshot);
                var secondSteps = FilterSecondSteps(first, secondCandidates);
                var second = actionSelector.Select(secondSteps);
                if (second == null)
                    continue;

                // Stage 10: не допускаем цепочки, где после шага 1 есть близкая угроза,
                // а шаг 2 не является защитным действием.
                if (HasImminentSameLaneThreat(projectedSnapshot) &&
                    second.Rank != DecisionRank.ThreatSafety)
                    continue;

                int totalProfit = first.ProfitScore + second.ProfitScore;
                DecisionRank bestRank = first.Rank < second.Rank ? first.Rank : second.Rank;

                result.Add(new ChainCandidate
                {
                    FirstStep = first,
                    SecondStep = second,
                    TotalEnergyCost = first.EnergyCost + second.EnergyCost,
                    TotalProfitScore = totalProfit,
                    BestRank = bestRank
                });
            }

            result.Sort(CompareCandidates);
            return result;
        }

        private static bool IsFirstStepEligible(BotSceneSnapshot snapshot, ChainStep first)
        {
            if (first == null)
                return false;
            if (first.TargetObstacle.Category == ObjectCategory.Neutral)
                return false;
            if (!IsChainAction(first.Action))
                return false;

            if (first.TargetObstacle.Category == ObjectCategory.Threat)
            {
                if (!IsChainThreatType(first.TargetObstacle.Type))
                    return false;
                return IsOnSameLane(snapshot, first.TargetObstacle);
            }

            return true;
        }

        private static ProjectedState ProjectAfterFirstStep(BotSceneSnapshot snapshot, ChainStep first)
        {
            bool projectedHamsterOnBottom = snapshot.HamsterOnBottom;
            float projectedRightX;

            if (first.Action == BotAction.SwitchLane)
            {
                projectedHamsterOnBottom = !snapshot.HamsterOnBottom;
                float advanceDistance = first.TargetObstacle.DistanceToHamster - first.ExecuteAtDistance;
                if (advanceDistance < 0f)
                    advanceDistance = 0f;

                projectedRightX = snapshot.HamsterRightX + advanceDistance;
            }
            else
            {
                float travel = first.Action == BotAction.SuperJump ? SuperJumpLandingTravel : JumpLandingTravel;
                projectedRightX = first.TargetObstacle.RightX + (travel * LandingPostFactor);
            }

            var state = new ProjectedState
            {
                HamsterOnBottom = projectedHamsterOnBottom,
                HamsterRightX = projectedRightX,
                HamsterWidth = snapshot.HamsterWidth,
                Energy = snapshot.Energy - first.EnergyCost,
                RemainingObjects = new List<ObstacleInfo>()
            };

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obstacle = snapshot.VisibleObjects[i];
                if (obstacle.StableId == first.TargetObstacle.StableId && first.Action != BotAction.SwitchLane)
                    continue;
                if (obstacle.RightX < projectedRightX - PassedObstacleMargin)
                    continue;

                float newDistance = obstacle.LeftX - projectedRightX;
                state.RemainingObjects.Add(new ObstacleInfo(
                    obstacle.Type,
                    obstacle.IsTopLane,
                    obstacle.LeftX,
                    obstacle.RightX,
                    obstacle.CenterX,
                    newDistance,
                    ObjectCategory.Neutral,
                    obstacle.StableId));
            }

            return state;
        }

        private static BotSceneSnapshot BuildProjectedSnapshot(ProjectedState state)
        {
            return new BotSceneSnapshot
            {
                HamsterOnBottom = state.HamsterOnBottom,
                HamsterOnRoof = false,
                HamsterRightX = state.HamsterRightX,
                HamsterWidth = state.HamsterWidth,
                Energy = state.Energy,
                Lives = 1,
                SnapshotTime = 0f,
                VisibleObjects = state.RemainingObjects
            };
        }

        private static List<ChainStep> FilterSecondSteps(
            ChainStep first,
            List<ChainStep> secondCandidates)
        {
            var result = new List<ChainStep>();
            if (secondCandidates == null)
                return result;

            for (int i = 0; i < secondCandidates.Count; i++)
            {
                var second = secondCandidates[i];
                if (second == null)
                    continue;
                if (second.TargetObstacle.StableId == first.TargetObstacle.StableId)
                    continue;
                if (second.TargetObstacle.Category == ObjectCategory.Neutral)
                    continue;
                if (!IsChainAction(second.Action))
                    continue;

                if (second.TargetObstacle.Category == ObjectCategory.Threat &&
                    !IsChainThreatType(second.TargetObstacle.Type))
                    continue;

                result.Add(second);
            }

            return result;
        }

        private static int CompareCandidates(ChainCandidate a, ChainCandidate b)
        {
            if (a.BestRank != b.BestRank)
                return a.BestRank.CompareTo(b.BestRank);

            if (a.TotalProfitScore != b.TotalProfitScore)
                return b.TotalProfitScore.CompareTo(a.TotalProfitScore);

            if (a.TotalEnergyCost != b.TotalEnergyCost)
                return a.TotalEnergyCost.CompareTo(b.TotalEnergyCost);

            return a.FirstStep.ExecuteAtDistance.CompareTo(b.FirstStep.ExecuteAtDistance);
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

        private static bool IsChainThreatType(ObstacleTypeEnum type)
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