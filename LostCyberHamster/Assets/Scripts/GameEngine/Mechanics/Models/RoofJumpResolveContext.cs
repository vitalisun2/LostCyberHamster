namespace Assets.Scripts.GameEngine.Mechanics.Models
{
    public readonly struct RoofJumpResolveContext
    {
        public readonly bool IsBottomLine;
        public readonly float HamsterLeftX;
        public readonly float HamsterRightX;
        public readonly float HamsterCenterX;
        public readonly float HamsterWidth;
        public readonly float RoofJumpShift;
        public readonly float JumpFromRoofShift;
        public readonly float ReachShift;

        public RoofJumpResolveContext(
            bool isBottomLine,
            float hamsterLeftX,
            float hamsterRightX,
            float hamsterCenterX,
            float hamsterWidth,
            float roofJumpShift,
            float jumpFromRoofShift)
        {
            IsBottomLine = isBottomLine;
            HamsterLeftX = hamsterLeftX;
            HamsterRightX = hamsterRightX;
            HamsterCenterX = hamsterCenterX;
            HamsterWidth = hamsterWidth;
            RoofJumpShift = roofJumpShift;
            JumpFromRoofShift = jumpFromRoofShift;
            ReachShift = roofJumpShift > jumpFromRoofShift ? roofJumpShift : jumpFromRoofShift;
        }
    }
}
