namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Физические константы бота, общие для нескольких подсистем.
    /// Единый source of truth — избегаем дублирования между planning и execution.
    /// Значения соответствуют runtime-анимациям (TapMechanics, JumpMechanics).
    /// </summary>
    internal static class BotPhysicsConsts
    {
        /// <summary>Длительность анимации смены линии (секунды).</summary>
        public const float LaneSwitchDuration = 0.3f;

        /// <summary>Длительность возврата управления после смены линии (секунды).</summary>
        public const float ReturnControlDuration = 0.47f;

        /// <summary>Базовая скорость игры (world units / секунда).</summary>
        public const float GameSpeedBase = Assets.Scripts.Consts.GameSpeedBase;

        /// <summary>Расстояние, которое проходит мир за transit + return control.</summary>
        public const float SwitchLaneFullTravel =
            (LaneSwitchDuration + ReturnControlDuration) * GameSpeedBase;

        /// <summary>Расстояние, которое проходит мир за return control phase.</summary>
        public const float SwitchLaneReturnControlTravel =
            ReturnControlDuration * GameSpeedBase;

        /// <summary>Примерное расстояние, на которое хомяк улетает при Jump (world units).</summary>
        public const float JumpLandingOffset = 3.8f;

        /// <summary>Padding вокруг коллайдера для safety проверок.</summary>
        public const float SafetyPadding = 0.15f;
    }
}
