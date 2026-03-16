namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Кандидат двухшаговой цепочки Stage 9.
    /// Хранит шаг исполнения сейчас и ожидаемый следующий шаг после проекции.
    /// </summary>
    public class ChainCandidate
    {
        public ChainStep FirstStep;
        public ChainStep SecondStep;
        public int TotalEnergyCost;
    }
}