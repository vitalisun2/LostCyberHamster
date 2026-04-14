namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Общее состояние хомяка, разделяемое между snapshot и planner state.
    /// </summary>
    public abstract class BotStateBase
    {
        public bool HamsterOnBottom;
        public bool HamsterOnRoof;
        public float HamsterRightX;
        public float HamsterWidth;
        public int Energy;
        public int Lives;

        internal void CopyFrom(BotStateBase source)
        {
            HamsterOnBottom = source.HamsterOnBottom;
            HamsterOnRoof = source.HamsterOnRoof;
            HamsterRightX = source.HamsterRightX;
            HamsterWidth = source.HamsterWidth;
            Energy = source.Energy;
            Lives = source.Lives;
        }
    }
}
