using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts;
using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;
using Assets.Scripts.Gameplay.Enums;
using NUnit.Framework;

namespace Assets.Tests.EditMode
{
    public class ObstacleLaneResolverTests
    {
        [Test]
        public void TryResolveIsTop_ResolvesRoadAnchors()
        {
            AssertResolves(Consts.ObstacleY0Pos, true);
            AssertResolves(Consts.ObstacleY1Pos, false);
        }

        [Test]
        public void TryResolveIsTop_ResolvesBigRoofAnchors()
        {
            AssertResolves(GetRoofY(Consts.ObstacleY0Pos, Consts.BIG_NOTALIVE_HEIGHT_UNITS), true);
            AssertResolves(GetRoofY(Consts.ObstacleY1Pos, Consts.BIG_NOTALIVE_HEIGHT_UNITS), false);
        }

        [Test]
        public void TryResolveIsTop_ResolvesMediumRoofAnchors()
        {
            AssertResolves(GetRoofY(Consts.ObstacleY0Pos, Consts.MEDIUM_NOTALIVE_HEIGHT_UNITS), true);
            AssertResolves(GetRoofY(Consts.ObstacleY1Pos, Consts.MEDIUM_NOTALIVE_HEIGHT_UNITS), false);
        }

        [Test]
        public void TryResolveIsTop_ReturnsFalse_WhenOutsideKnownAnchorsTolerance()
        {
            bool resolved = ObstacleLaneResolver.TryResolveIsTop(10f, out _);

            Assert.IsFalse(resolved);
        }

        [Test]
        public void IsBottomLineCloser_UsesSameAnchorsAsRuntimeResolver()
        {
            Assert.IsFalse(ObstacleLaneResolver.IsBottomLineCloser(
                GetRoofY(Consts.ObstacleY0Pos, Consts.MEDIUM_NOTALIVE_HEIGHT_UNITS)));
            Assert.IsTrue(ObstacleLaneResolver.IsBottomLineCloser(
                GetRoofY(Consts.ObstacleY1Pos, Consts.MEDIUM_NOTALIVE_HEIGHT_UNITS)));
        }

        [Test]
        public void ResolveSortingLayerName_UsesRoofAwareLaneAnchors()
        {
            Assert.AreEqual(
                ObstacleLaneResolver.UpperSpritesSortingLayer,
                ObstacleLaneResolver.ResolveSortingLayerName(
                    GetRoofY(Consts.ObstacleY0Pos, Consts.BIG_NOTALIVE_HEIGHT_UNITS)));

            Assert.AreEqual(
                ObstacleLaneResolver.UpperSpritesSortingLayer,
                ObstacleLaneResolver.ResolveSortingLayerName(
                    GetRoofY(Consts.ObstacleY0Pos, Consts.MEDIUM_NOTALIVE_HEIGHT_UNITS)));

            Assert.AreEqual(
                ObstacleLaneResolver.LowerSpritesSortingLayer,
                ObstacleLaneResolver.ResolveSortingLayerName(
                    GetRoofY(Consts.ObstacleY1Pos, Consts.BIG_NOTALIVE_HEIGHT_UNITS)));

            Assert.AreEqual(
                ObstacleLaneResolver.LowerSpritesSortingLayer,
                ObstacleLaneResolver.ResolveSortingLayerName(
                    GetRoofY(Consts.ObstacleY1Pos, Consts.MEDIUM_NOTALIVE_HEIGHT_UNITS)));
        }

        private static void AssertResolves(float yPosition, bool expectedIsTop)
        {
            bool resolved = ObstacleLaneResolver.TryResolveIsTop(yPosition, out bool isTop);

            Assert.IsTrue(resolved);
            Assert.AreEqual(expectedIsTop, isTop);
        }

        private static float GetRoofY(float roadY, float roofHeight)
        {
            return roadY + roofHeight + Consts.RoofOffset;
        }
    }

    public class DecisionPointDetectorTests
    {
        [Test]
        public void TryDetectRequiredDecisionPoint_BuildsChainWithOccupiedRoof_WhenRoofHasDamagingOccupant()
        {
            var detector = new DecisionPointDetector();
            var hamster = CreateHamsterSnapshot(isOnBottomLine: false);
            var roofObstacle = CreateObstacleSnapshot(101, ObstacleTypeEnum.mediumNotAlive, 0f, 3.4f, isTopLine: true);
            var roofOccupant = CreateObstacleSnapshot(102, ObstacleTypeEnum.smallNotAliveRoadAndRoof, 0.8f, 1.6f, isTopLine: true);
            var worldSnapshot = CreateWorldSnapshot(hamster, roofObstacle, roofOccupant);

            bool detected = detector.TryDetectRequiredDecisionPoint(new PlanningState(hamster, 0, 0f), worldSnapshot, out DecisionPoint decisionPoint);

            Assert.IsTrue(detected);
            Assert.NotNull(decisionPoint);
            Assert.AreEqual(roofObstacle.InstanceId, decisionPoint.Chain.FirstObstacle.InstanceId);
            Assert.AreEqual(2, decisionPoint.Chain.Count);
            Assert.IsTrue(decisionPoint.Chain.HasDamagingRoofOccupant(0));
        }

        [Test]
        public void TryDetectRequiredDecisionPoint_BuildsClearRoofChain_WhenRoofIsClear()
        {
            var detector = new DecisionPointDetector();
            var hamster = CreateHamsterSnapshot(isOnBottomLine: false);
            var roofObstacle = CreateObstacleSnapshot(201, ObstacleTypeEnum.mediumNotAlive, 0f, 3.4f, isTopLine: true);
            var worldSnapshot = CreateWorldSnapshot(hamster, roofObstacle);

            bool detected = detector.TryDetectRequiredDecisionPoint(new PlanningState(hamster, 0, 0f), worldSnapshot, out DecisionPoint decisionPoint);

            Assert.IsTrue(detected);
            Assert.NotNull(decisionPoint);
            Assert.AreEqual(roofObstacle.InstanceId, decisionPoint.Chain.FirstObstacle.InstanceId);
            Assert.AreEqual(1, decisionPoint.Chain.Count);
            Assert.IsTrue(decisionPoint.Chain.TryFindFirstRoof(out _, out int roofWorldIndex, out int roofChainIndex));
            Assert.AreEqual(0, roofWorldIndex);
            Assert.AreEqual(0, roofChainIndex);
            Assert.IsFalse(decisionPoint.Chain.HasDamagingRoofOccupant(0));
        }

        private static HamsterSnapshot CreateHamsterSnapshot(bool isOnBottomLine)
        {
            return new HamsterSnapshot(
                HamsterStateEnum.Run,
                isOnBottomLine,
                isOnRoof: false,
                energy: 100,
                lives: 3,
                isShifting: false,
                roofSupportInstanceId: null,
                hamsterLeftX: -3.2f,
                hamsterRightX: -2.7f,
                hamsterBottomY: 0f,
                hamsterTopY: 1f);
        }

        private static ObstacleSnapshot CreateObstacleSnapshot(
            int instanceId,
            ObstacleTypeEnum obstacleType,
            float leftX,
            float rightX,
            bool isTopLine)
        {
            return new ObstacleSnapshot(
                instanceId,
                obstacleType,
                isTopLine,
                leftX,
                rightX,
                centerX: (leftX + rightX) * 0.5f,
                bottomY: 0f,
                topY: 1f);
        }

        private static WorldSnapshot CreateWorldSnapshot(
            HamsterSnapshot hamster,
            params ObstacleSnapshot[] obstacles)
        {
            return new WorldSnapshot(
                hamster,
                new List<ObstacleSnapshot>(obstacles),
                screenLeftEdgeX: -10f,
                screenRightEdgeX: 10f,
                visionRightEdgeX: 20f,
                snapshotTime: 0f);
        }
    }
}
