namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Физические константы бота, общие для нескольких подсистем.
    /// Единый source of truth — избегаем дублирования между planning и execution.
    /// Значения соответствуют runtime-анимациям (TapMechanics, JumpMechanics).
    /// </summary>
    internal static class BotPhysicsConsts
    {
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
    }
}
