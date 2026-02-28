using Assets.Scripts.Bot;

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
    /// Оценщик по умолчанию: взвешенная сумма survival + energy + collectibles + position + ulta.
    /// </summary>
    public class DefaultStateEvaluator : IStateEvaluator
    {
        private readonly float _wSurvival;
        private readonly float _wEnergy;
        private readonly float _wCollectibles;
        private readonly float _wPosition;
        private readonly float _wUlta;

        public DefaultStateEvaluator(
            float wSurvival = 10f,
            float wEnergy = 3f,
            float wCollectibles = 2f,
            float wPosition = 1f,
            float wUlta = 2f)
        {
            _wSurvival = wSurvival;
            _wEnergy = wEnergy;
            _wCollectibles = wCollectibles;
            _wPosition = wPosition;
            _wUlta = wUlta;
        }

        public DefaultStateEvaluator(BotStrategyConfig config)
            : this(config.WeightSurvival, config.WeightEnergy,
                   config.WeightCollectibles, config.WeightPosition) { }

        public DefaultStateEvaluator(BotPlayStyleConfig config)
            : this(config.WeightSurvival, config.WeightEnergy,
                   config.WeightCollectibles, config.WeightPosition, config.WeightUlta) { }

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
            score += (state.UltaCharge / 100f) * _wUlta;

            return score;
        }
    }
}
