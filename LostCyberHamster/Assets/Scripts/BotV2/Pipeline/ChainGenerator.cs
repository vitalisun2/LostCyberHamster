using System.Collections.Generic;
using Assets.Scripts.Common.Models;

namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Единый планировщик цепочек.
    /// Каждый кандидат оборачивается в ChainCandidate.
    /// Если для шага удаётся спроецировать валидный второй шаг — создаётся двухшаговая цепочка.
    /// Иначе — одношаговая (SecondStep = null). Оркестратор всегда работает с цепочками.
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
                if (first == null)
                    continue;

                ChainStep second = TryBuildSecondStep(
                    snapshot, first, classifier, actionGenerator, actionSelector);

                int totalProfit = first.ProfitScore + (second?.ProfitScore ?? 0);
                DecisionRank bestRank = second != null && second.Rank < first.Rank
                    ? second.Rank
                    : first.Rank;

                result.Add(new ChainCandidate
                {
                    FirstStep = first,
                    SecondStep = second,
                    SecondStepUsesProjectedCoordinates = second != null,
                    TotalEnergyCost = first.EnergyCost + (second?.EnergyCost ?? 0),
                    TotalProfitScore = totalProfit,
                    BestRank = bestRank
                });
            }

            result.Sort(CompareCandidates);
            return result;
        }

        private ChainStep TryBuildSecondStep(
            BotSceneSnapshot snapshot,
            ChainStep first,
            ObjectClassifier classifier,
            ActionGenerator actionGenerator,
            ActionSelector actionSelector)
        {
            if (!IsEligibleForSecondStepProjection(snapshot, first))
                return null;

            var projected = ProjectAfterFirstStep(snapshot, first);
            var projectedSnapshot = BuildProjectedSnapshot(projected);
            classifier.Classify(projectedSnapshot);

            var secondCandidates = actionGenerator.Generate(projectedSnapshot);
            var secondSteps = FilterSecondSteps(first, secondCandidates);
            var second = actionSelector.Select(secondSteps);
            if (second == null)
                return null;

            // Не допускаем второй шаг, если после первого есть близкая угроза,
            // а второй шаг не является защитным действием.
            if (HasImminentSameLaneThreat(projectedSnapshot) &&
                second.Rank != DecisionRank.ThreatSafety)
                return null;

            return second;
        }

        /// <summary>
        /// Определяет, стоит ли пытаться проецировать второй шаг.
        /// Нейтральные объекты и неподдерживаемые действия не нуждаются в проекции.
        /// Крупные угрозы (big/medium) исключены: их проекция после прыжка слишком ненадёжна.
        /// </summary>
        private static bool IsEligibleForSecondStepProjection(BotSceneSnapshot snapshot, ChainStep first)
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

        private static int CompareCandidates(ChainCandidate a, ChainCandidate b)
        {
            // Сначала по первому шагу: ранг → профит → стоимость → дистанция.
            int cmp = ChainStep.ComparePriority(a.FirstStep, b.FirstStep);
            if (cmp != 0)
                return -cmp; // ComparePriority: >0 = a лучше, Sort хочет <0 = a раньше

            // При равных первых шагах: наличие второго шага лучше.
            bool aHas = a.SecondStep != null;
            bool bHas = b.SecondStep != null;
            if (aHas != bHas)
                return aHas ? -1 : 1;

            // Оба с вторым шагом — сравниваем суммарные метрики.
            if (aHas)
            {
                if (a.TotalProfitScore != b.TotalProfitScore)
                    return b.TotalProfitScore.CompareTo(a.TotalProfitScore);

                if (a.TotalEnergyCost != b.TotalEnergyCost)
                    return a.TotalEnergyCost.CompareTo(b.TotalEnergyCost);
            }

            return 0;
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