namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Единый файл констант бота.
    /// Разделён по секциям: физика и runtime-исполнение.
    /// </summary>
    internal static class BotConsts
    {
        // Physics
        /// <summary>
        /// Runtime decision-ready длительность SwitchLane.
        /// Должна совпадать с transition duration в ShiftTransformAnimator.controller.
        /// </summary>
        public const float SwitchLaneDecisionDuration = 0.45f;

        /// <summary>Базовая скорость игры (world units / секунда).</summary>
        public const float GameSpeedBase = Assets.Scripts.Consts.GameSpeedBase;

        /// <summary>
        /// Сдвиг мира к моменту, когда runtime снова разрешает новый выбор после SwitchLane.
        /// </summary>
        public const float SwitchLaneDecisionTravel =
            SwitchLaneDecisionDuration * GameSpeedBase;

        /// <summary>Примерное расстояние, на которое хомяк улетает при Jump (world units).</summary>
        public const float JumpLandingOffset = 3.8f;

        /// <summary>Расстояние полёта SuperJump: клип 1.2s × GameSpeedBase 3.8 = 4.56 world units.</summary>
        public const float SuperJumpLandingOffset = 4.56f;

        /// <summary>Суммарный расход энергии на SuperJump: JumpRequest (10) + SuperJumpRequest (10).</summary>
        public const int SuperJumpEnergyCost = 20;

        // Execution
        public const float JumpLateFallbackDistance = 0.1f;
        public const float StepTooLateThreshold = -0.3f;
        public const float SwitchLaneMinElapsed = 0.1f;

        public const string JumpClipName = "transform_jump";
    }
}
