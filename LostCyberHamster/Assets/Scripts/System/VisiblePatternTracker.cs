using System.Collections.Generic;
using Assets.Scripts.GameEngine.Mechanics;

namespace Assets.Scripts.System
{
    public sealed class VisiblePatternTracker
    {
        private const float _edgeTolerance = 0.01f;

        private readonly List<PatternXRange> _ranges = new();

        public void Clear()
        {
            _ranges.Clear();
        }

        public void RegisterPattern(int patternIndex, float leftEdge, float rightEdge)
        {
            if (leftEdge > rightEdge)
            {
                (leftEdge, rightEdge) = (rightEdge, leftEdge);
            }

            _ranges.Add(new PatternXRange(patternIndex, leftEdge, rightEdge));
        }

        public void Update(float deltaTime, float screenLeftEdge)
        {
            if (_ranges.Count == 0)
            {
                return;
            }

            float shift = Consts.RoadScrollSpeed * ScrollLeftMechanics.SpeedMultiplier * deltaTime;
            for (int i = 0; i < _ranges.Count; i++)
            {
                _ranges[i].ShiftLeft(shift);
            }

            Prune(screenLeftEdge);
        }

        public int GetCurrentPatternIndex(
            float playerLeftX,
            float playerRightX,
            float screenLeftEdge,
            float screenRightEdge)
        {
            if (_ranges.Count == 0)
            {
                return -1;
            }

            if (playerLeftX > playerRightX)
            {
                (playerLeftX, playerRightX) = (playerRightX, playerLeftX);
            }

            int overlappingPatternIndex = -1;
            float overlappingPatternLeftEdge = float.PositiveInfinity;
            int upcomingPatternIndex = -1;
            float upcomingPatternLeftEdge = float.PositiveInfinity;
            int trailingPatternIndex = -1;
            float trailingPatternRightEdge = float.NegativeInfinity;

            foreach (var range in _ranges)
            {
                bool isVisible =
                    range.LeftEdge <= screenRightEdge + _edgeTolerance &&
                    range.RightEdge >= screenLeftEdge - _edgeTolerance;

                if (!isVisible)
                {
                    continue;
                }

                bool overlapsPlayer =
                    range.LeftEdge <= playerRightX + _edgeTolerance &&
                    range.RightEdge >= playerLeftX - _edgeTolerance;

                if (overlapsPlayer)
                {
                    if (range.LeftEdge < overlappingPatternLeftEdge)
                    {
                        overlappingPatternLeftEdge = range.LeftEdge;
                        overlappingPatternIndex = range.PatternIndex;
                    }

                    continue;
                }

                bool isNotPassedByPlayer = range.RightEdge >= playerLeftX - _edgeTolerance;
                if (isNotPassedByPlayer)
                {
                    if (range.LeftEdge < upcomingPatternLeftEdge)
                    {
                        upcomingPatternLeftEdge = range.LeftEdge;
                        upcomingPatternIndex = range.PatternIndex;
                    }

                    continue;
                }

                if (range.RightEdge > trailingPatternRightEdge)
                {
                    trailingPatternRightEdge = range.RightEdge;
                    trailingPatternIndex = range.PatternIndex;
                }
            }

            if (overlappingPatternIndex >= 0)
            {
                return overlappingPatternIndex;
            }

            if (upcomingPatternIndex >= 0)
            {
                return upcomingPatternIndex;
            }

            return trailingPatternIndex;
        }

        private void Prune(float screenLeftEdge)
        {
            _ranges.RemoveAll(range => range.RightEdge < screenLeftEdge - _edgeTolerance);
        }

        private sealed class PatternXRange
        {
            public PatternXRange(int patternIndex, float leftEdge, float rightEdge)
            {
                PatternIndex = patternIndex;
                LeftEdge = leftEdge;
                RightEdge = rightEdge;
            }

            public int PatternIndex { get; }
            public float LeftEdge { get; private set; }
            public float RightEdge { get; private set; }

            public void ShiftLeft(float shift)
            {
                LeftEdge -= shift;
                RightEdge -= shift;
            }
        }
    }
}
