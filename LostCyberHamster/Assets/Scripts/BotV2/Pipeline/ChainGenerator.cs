using System.Collections.Generic;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Stage 9: генерация двухшаговых цепочек на одной линии.
    /// Использует one-step ActionGenerator поверх проецированного состояния и выбирает валидный второй шаг.
    /// </summary>
    public class ChainGenerator
    {
        private const float JumpLandingTravel = 3.8f;
        private const float SuperJumpLandingTravel = 4.6f;
        private const float LandingPostFactor = 0.4f;
        private const float PassedObstacleMargin = 0.4f;

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
                var secondThreatSteps = FilterSecondThreatSteps(projectedSnapshot, first, secondCandidates);
                var second = actionSelector.Select(secondThreatSteps);
                if (second == null)
                    continue;

                result.Add(new ChainCandidate
                {
                    FirstStep = first,
                    SecondStep = second,
                    TotalEnergyCost = first.EnergyCost + second.EnergyCost
                });
            }

            result.Sort(CompareCandidates);
            return result;
        }

        private static bool IsFirstStepEligible(BotSceneSnapshot snapshot, ChainStep first)
        {
            if (first == null)
                return false;
            if (first.TargetObstacle.Category != ObjectCategory.Threat)
                return false;
            if (!IsChainThreatType(first.TargetObstacle.Type))
                return false;
            if (first.Action != BotAction.Jump && first.Action != BotAction.SuperJump)
                return false;

            return IsOnSameLane(snapshot, first.TargetObstacle);
        }

        private static ProjectedState ProjectAfterFirstStep(BotSceneSnapshot snapshot, ChainStep first)
        {
            float travel = first.Action == BotAction.SuperJump ? SuperJumpLandingTravel : JumpLandingTravel;
            float projectedRightX = first.TargetObstacle.RightX + (travel * LandingPostFactor);

            var state = new ProjectedState
            {
                HamsterOnBottom = snapshot.HamsterOnBottom,
                HamsterRightX = projectedRightX,
                HamsterWidth = snapshot.HamsterWidth,
                Energy = snapshot.Energy - first.EnergyCost,
                RemainingObjects = new List<ObstacleInfo>()
            };

            for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
            {
                var obstacle = snapshot.VisibleObjects[i];
                if (obstacle.StableId == first.TargetObstacle.StableId)
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

        private static List<ChainStep> FilterSecondThreatSteps(
            BotSceneSnapshot projectedSnapshot,
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
                if (second.TargetObstacle.Category != ObjectCategory.Threat)
                    continue;
                if (!IsChainThreatType(second.TargetObstacle.Type))
                    continue;
                if (second.Action != BotAction.Jump && second.Action != BotAction.SuperJump)
                    continue;
                if (!IsOnSameLane(projectedSnapshot, second.TargetObstacle))
                    continue;

                result.Add(second);
            }

            return result;
        }

        private static int CompareCandidates(ChainCandidate a, ChainCandidate b)
        {
            if (a.SecondStep.Rank != b.SecondStep.Rank)
                return a.SecondStep.Rank.CompareTo(b.SecondStep.Rank);

            int profitA = a.FirstStep.ProfitScore + a.SecondStep.ProfitScore;
            int profitB = b.FirstStep.ProfitScore + b.SecondStep.ProfitScore;
            if (profitA != profitB)
                return profitB.CompareTo(profitA);

            if (a.TotalEnergyCost != b.TotalEnergyCost)
                return a.TotalEnergyCost.CompareTo(b.TotalEnergyCost);

            return a.FirstStep.ExecuteAtDistance.CompareTo(b.FirstStep.ExecuteAtDistance);
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
    }
}