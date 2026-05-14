namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.RoofJumpOver
{
    /// <summary>
    /// Хранит runtime-дистанции прыжка по крыше и fallback-прыжка с крыши.
    /// </summary>
    internal readonly struct RoofJumpOverTravel
    {
        public RoofJumpOverTravel(float roofJumpTravel, float jumpFromRoofTravel)
        {
            RoofJumpTravel = roofJumpTravel;
            JumpFromRoofTravel = jumpFromRoofTravel;
        }

        public float RoofJumpTravel { get; }
        public float JumpFromRoofTravel { get; }
    }
}