namespace Assets.Scripts.Bot.Strategies.Shared.RoofJumpOver
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

        /// <summary>
        /// Дистанция roof jump части action.
        /// </summary>
        public float RoofJumpTravel { get; }

        /// <summary>
        /// Дистанция fallback jump-from-roof для runtime resolver.
        /// </summary>
        public float JumpFromRoofTravel { get; }
    }
}
