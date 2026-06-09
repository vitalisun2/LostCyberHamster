namespace Assets.Scripts.Bot.Strategies.Shared.JumpOn
{
    /// <summary>
    /// Хранит дистанции ground jump-on: отдельно runtime resolver-точку и полный action до возврата в Run.
    /// </summary>
    internal readonly struct JumpOnTravel
    {
        public JumpOnTravel(
            float actionTravel,
            float resolveTravel,
            float resolveFireShiftOffset)
        {
            ActionTravel = actionTravel;
            ResolveTravel = resolveTravel;
            ResolveFireShiftOffset = resolveFireShiftOffset;
        }

        /// <summary>
        /// Дистанция мира до полного завершения action и возврата в Run.
        /// </summary>
        public float ActionTravel { get; }

        /// <summary>
        /// Дистанция мира до точки, в которой runtime resolver определяет результат прыжка.
        /// </summary>
        public float ResolveTravel { get; }

        /// <summary>
        /// Смещение fire shift между полным action и resolver-точкой.
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
