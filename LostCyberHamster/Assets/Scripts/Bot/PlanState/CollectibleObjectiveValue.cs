namespace Assets.Scripts.Bot.PlanState
{
    /// <summary>
    /// Хранит полезность collectable objective для planning-ветки.
    /// </summary>
    public readonly struct CollectibleObjectiveValue
    {
        /// <summary>
        /// Пустое значение collectable objective.
        /// </summary>
        public static CollectibleObjectiveValue None { get; } = new CollectibleObjectiveValue(
            CollectibleKind.None,
            0);

        /// <summary>
        /// Создает planning-value для collectable objective.
        /// </summary>
        public CollectibleObjectiveValue(
            CollectibleKind kind,
            int effectiveGain)
        {
            Kind = kind;
            EffectiveGain = effectiveGain;
        }

        /// <summary>
        /// Тип collectable objective.
        /// </summary>
        public CollectibleKind Kind { get; }

        /// <summary>
        /// Реальная польза collectable с учетом caps текущего projected state.
        /// </summary>
        public int EffectiveGain { get; }

        /// <summary>
        /// Возвращает true, если collectable имеет положительную planning-ценность.
        /// </summary>
        public bool HasValue => Kind != CollectibleKind.None && EffectiveGain > 0;
    }
}
