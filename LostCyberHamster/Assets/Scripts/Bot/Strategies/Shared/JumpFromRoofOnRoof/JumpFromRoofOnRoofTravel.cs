namespace Assets.Scripts.Bot.Strategies.Shared.JumpFromRoofOnRoof
{
    /// <summary>
    /// Хранит runtime-дистанции для прыжка с текущей крыши на следующую крышу.
    /// </summary>
    internal readonly struct JumpFromRoofOnRoofTravel
    {
        public JumpFromRoofOnRoofTravel(
            float runFromRoofTravel,
            float roofJumpTravel,
            float jumpFromRoofTravel)
        {
            RunFromRoofTravel = runFromRoofTravel;
            RoofJumpTravel = roofJumpTravel;
            JumpFromRoofTravel = jumpFromRoofTravel;
        }

        /// <summary>
        /// Дистанция автоматического схода с крыши.
        /// </summary>
        public float RunFromRoofTravel { get; }

        /// <summary>
        /// Дистанция roof-jump части, по которой resolver проверяет посадку на крышу.
        /// </summary>
        public float RoofJumpTravel { get; }

        /// <summary>
        /// Дистанция fallback jump-from-roof, передаваемая resolver-у для проверки outcome.
        /// </summary>
        public float JumpFromRoofTravel { get; }
    }
}
