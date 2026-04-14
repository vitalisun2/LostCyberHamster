namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Единый файл констант бота.
    /// Разделён по секциям: физика и runtime-исполнение.
    /// </summary>
    internal static class BotConsts
    {
        // Planning
        public const int MaxBranchDepth = 5;
        public const float PlannerExtraRightVisionScreenFraction = 0.5f;
        public const float SwitchLaneWindowSelectionRatio = 0.5f;

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

        /// <summary>Имя клипа SuperJump для получения runtime shift через GetWorldShiftForClip.</summary>
        public const string SuperJumpClipName = "transform_super_jump";

        /// <summary>Имя клипа JumpOnRoof для получения runtime shift через GetWorldShiftForClip.</summary>
        public const string JumpOnRoofClipName = "transform_jump_on_roof";

        /// <summary>
        /// Fallback для JumpOnRoof landing offset в EditMode-тестах, где нет runtime AnimatorController.
        /// В runtime не используется — BotOrchestrator передаёт точное значение из клипа.
        /// </summary>
        public const float JumpOnRoofLandingOffsetFallback = 3.8f;

        /// <summary>
        /// Fallback для SuperJump landing offset в EditMode-тестах, где нет runtime AnimatorController.
        /// В runtime не используется — BotOrchestrator передаёт точное значение из клипа.
        /// </summary>
        public const float SuperJumpLandingOffsetFallback = 4.56f;

        /// <summary>Стоимость энергии за обычный Jump.</summary>
        public const int JumpEnergyCost = 10;

        /// <summary>Дополнительная стоимость энергии за SuperJump (второй тап поверх Jump).</summary>
        public const int SuperJumpAdditionalEnergyCost = 10;

        /// <summary>Суммарный расход энергии на SuperJump: Jump + дополнительное усилие.</summary>
        public const int SuperJumpEnergyCost = JumpEnergyCost + SuperJumpAdditionalEnergyCost;

        // Execution
        public const float JumpLateFallbackDistance = 0.1f;
        public const float StepTooLateThreshold = -0.3f;
        public const float SwitchLaneMinElapsed = 0.1f;

        public const string JumpClipName = "transform_jump";
    }
}
