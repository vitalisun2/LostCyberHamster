namespace Assets.Scripts.Bot.StrategiesNew.Shared.JumpFromRoof
{
    /// <summary>
    /// Хранит runtime-дистанции автоматического схода с крыши и прыжка с крыши.
    /// </summary>
    internal readonly struct JumpFromRoofTravel
    {
        public JumpFromRoofTravel(
            float runFromRoofTravel,
            float roofJumpTravel,
            float jumpFromRoofTravel)
        {
            RunFromRoofTravel = runFromRoofTravel;
            RoofJumpTravel = roofJumpTravel;
            ActionTravel = jumpFromRoofTravel;
        }

        /// <summary>
        /// Дистанция автоматического схода с крыши.
        /// </summary>
        public float RunFromRoofTravel { get; }

        /// <summary>
        /// Дистанция roof jump части runtime-прыжка.
        /// </summary>
        public float RoofJumpTravel { get; }

        /// <summary>
        /// Полная дистанция прыжка с крыши до возврата на дорогу.
        /// </summary>
        public float ActionTravel { get; }
    }
}
