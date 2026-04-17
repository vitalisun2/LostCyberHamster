using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Perception
{
    public sealed class BotStateSnapshot
    {
        public BotStateSnapshot(
            HamsterStateEnum hamsterState,
            bool isOnBottomLine,
            bool isOnRoof,
            int energy,
            int lives,
            bool isDamaged,
            bool isShifting,
            int? roofSupportInstanceId,
            float hamsterLeftX,
            float hamsterRightX)
        {
            HamsterState = hamsterState;
            IsOnBottomLine = isOnBottomLine;
            IsOnRoof = isOnRoof;
            Energy = energy;
            Lives = lives;
            IsDamaged = isDamaged;
            IsShifting = isShifting;
            RoofSupportInstanceId = roofSupportInstanceId;
            HamsterLeftX = hamsterLeftX;
            HamsterRightX = hamsterRightX;
        }

        public HamsterStateEnum HamsterState { get; }
        public bool IsOnBottomLine { get; }
        public bool IsOnRoof { get; }
        public int Energy { get; }
        public int Lives { get; }
        public bool IsDamaged { get; }
        public bool IsShifting { get; }
        public int? RoofSupportInstanceId { get; }
        public float HamsterLeftX { get; }
        public float HamsterRightX { get; }

        public BotStateSnapshot WithLine(bool isOnBottomLine, bool isOnRoof)
        {
            return new BotStateSnapshot(
                HamsterState,
                isOnBottomLine,
                isOnRoof,
                Energy,
                Lives,
                IsDamaged,
                IsShifting,
                RoofSupportInstanceId,
                HamsterLeftX,
                HamsterRightX);
        }
    }
}
