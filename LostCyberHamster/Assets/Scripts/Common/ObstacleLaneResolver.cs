using Assets.Scripts;
using UnityEngine;

namespace Assets.Scripts.Common
{
    public static class ObstacleLaneResolver
    {
        private enum Lane
        {
            Top,
            Bottom
        }

        private static readonly float[] TopLineAnchors = BuildLineAnchors(Consts.ObstacleY0Pos);
        private static readonly float[] BottomLineAnchors = BuildLineAnchors(Consts.ObstacleY1Pos);

        public static bool TryResolveIsTop(float yPosition, out bool isTop)
        {
            Lane nearestLane = GetNearestLane(yPosition, out float nearestDistance);
            isTop = nearestLane == Lane.Top;
            return nearestDistance <= Consts.ObstacleLineTolerance;
        }

        public static bool IsBottomLineCloser(float yPosition)
        {
            return GetNearestLane(yPosition, out _) == Lane.Bottom;
        }

        private static Lane GetNearestLane(float yPosition, out float nearestDistance)
        {
            float topDistance = GetMinDistance(yPosition, TopLineAnchors);
            float bottomDistance = GetMinDistance(yPosition, BottomLineAnchors);

            if (bottomDistance < topDistance)
            {
                nearestDistance = bottomDistance;
                return Lane.Bottom;
            }

            nearestDistance = topDistance;
            return Lane.Top;
        }

        private static float[] BuildLineAnchors(float roadY)
        {
            return new[]
            {
                roadY,
                GetRoofY(roadY, Consts.BIG_NOTALIVE_HEIGHT_UNITS),
                GetRoofY(roadY, Consts.MEDIUM_NOTALIVE_HEIGHT_UNITS)
            };
        }

        private static float GetRoofY(float roadY, float roofHeight)
        {
            return roadY + roofHeight + Consts.RoofOffset;
        }

        private static float GetMinDistance(float yPosition, float[] anchors)
        {
            float minDistance = float.MaxValue;

            for (int i = 0; i < anchors.Length; i++)
                minDistance = Mathf.Min(minDistance, Mathf.Abs(yPosition - anchors[i]));

            return minDistance;
        }
    }
}
