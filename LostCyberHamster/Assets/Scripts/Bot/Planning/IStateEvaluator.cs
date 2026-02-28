namespace Assets.Scripts.Bot.Planning
{
    /// <summary>
    /// Интерфейс оценки состояния мира (Strategy pattern).
    /// Используется BotPlanner для выбора лучшей ветви дерева.
    /// </summary>
    public interface IStateEvaluator
    {
        /// <summary>
        /// Оценить состояние мира. Больше = лучше.
        /// </summary>
        float Evaluate(ref SimWorldState state);
    }

    /// <summary>
    /// Оценщик по умолчанию: взвешенная сумма survival + energy + collectibles + position.
    /// </summary>
    public class DefaultStateEvaluator : IStateEvaluator
    {
        private readonly float _wSurvival;
        private readonly float _wEnergy;
        private readonly float _wCollectibles;
        private readonly float _wPosition;

        public DefaultStateEvaluator(
            float wSurvival = 10f,
            float wEnergy = 3f,
            float wCollectibles = 2f,
            float wPosition = 1f)
        {
            _wSurvival = wSurvival;
            _wEnergy = wEnergy;
            _wCollectibles = wCollectibles;
            _wPosition = wPosition;
        }

        public DefaultStateEvaluator(BotStrategyConfig config)
            : this(config.WeightSurvival, config.WeightEnergy,
                   config.WeightCollectibles, config.WeightPosition) { }

        public float Evaluate(ref SimWorldState state)
        {
            if (state.IsDead) return -10000f;

            float score = state.Score;

            // Survival: жизни
            score += state.Lives * _wSurvival;

            // Energy: больше энергии = больше возможностей
            score += (state.Energy / 100f) * _wEnergy;

            // Collectibles
            score += state.CoinsCollected * _wCollectibles;

            // Position bonus: нижняя линия лучше для контроля
            if (state.IsOnBottomLine)
                score += _wPosition;

            // Ulta bonus
            score += (state.UltaCharge / 100f) * 2f;

            return score;
        }
    }
}
