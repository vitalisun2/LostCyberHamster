namespace Assets.Scripts.GameEngine.Mechanics.Models
{
    public readonly struct JumpResolveContext
    {
        public readonly bool IsBottomLine;
        public readonly float HamsterLeftX;
        public readonly float HamsterRightX;
        public readonly float HamsterCenterX;
        public readonly float HamsterWidth;
        public readonly float JumpShift;
        public readonly float ReachShift;
        public readonly bool HasJumpMidY;
        public readonly float HamsterJumpMidBottomY;
        public readonly float HamsterJumpMidTopY;
        public readonly bool DamageBigAliveWithoutYByReach;

        public JumpResolveContext(
            bool isBottomLine,
            float hamsterLeftX,
            float hamsterRightX,
            float hamsterCenterX,
            float hamsterWidth,
            float jumpShift,
            float reachShift,
            bool hasJumpMidY = false,
            float hamsterJumpMidBottomY = 0f,
            float hamsterJumpMidTopY = 0f,
            bool damageBigAliveWithoutYByReach = false)
        {
            IsBottomLine = isBottomLine;
            HamsterLeftX = hamsterLeftX;
            HamsterRightX = hamsterRightX;
            HamsterCenterX = hamsterCenterX;
            HamsterWidth = hamsterWidth;
            JumpShift = jumpShift;
            ReachShift = reachShift;
            HasJumpMidY = hasJumpMidY;
            HamsterJumpMidBottomY = hamsterJumpMidBottomY;
            HamsterJumpMidTopY = hamsterJumpMidTopY;
            DamageBigAliveWithoutYByReach = damageBigAliveWithoutYByReach;
        }
    }
}
