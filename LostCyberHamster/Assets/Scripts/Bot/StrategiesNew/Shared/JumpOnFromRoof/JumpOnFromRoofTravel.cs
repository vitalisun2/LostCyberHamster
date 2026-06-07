namespace Assets.Scripts.Bot.StrategiesNew.Shared.JumpOnFromRoof
{
    /// <summary>
    /// Хранит runtime-дистанции roof-to-road jump-on действия.
    /// </summary>
    internal readonly struct JumpOnFromRoofTravel
    {
        public JumpOnFromRoofTravel(
            float runFromRoofTravel,
            float roofJumpTravel,
            float resolveTravel,
            float actionTravel,
            float resolveFireShiftOffset)
        {
            RunFromRoofTravel = runFromRoofTravel;
            RoofJumpTravel = roofJumpTravel;
            ResolveTravel = resolveTravel;
            ActionTravel = actionTravel;
            ResolveFireShiftOffset = resolveFireShiftOffset;
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
        /// Дистанция jump-from-roof части, по которой resolver проверяет напрыгивание на obstacle.
        /// </summary>
        public float ResolveTravel { get; }

        /// <summary>
        /// Полная дистанция до завершения jump-on-from-roof action и возврата в Run.
        /// </summary>
        public float ActionTravel { get; }

        /// <summary>
        /// Смещение между первым input action и моментом вызова runtime resolver-а.
        /// </summary>
        public float ResolveFireShiftOffset { get; }

        /// <summary>
        /// Возвращает world-shift до runtime resolver-точки.
        /// </summary>
        public float GetResolveFireShift(float fireShift)
        {
            return fireShift + ResolveFireShiftOffset;
        }
    }
}
