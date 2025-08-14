namespace Vues.GameCore
{
    /// <summary>
    /// Хранилище денег.
    /// </summary>
    public static class MoneyStorage
    {
        /// <summary>
        /// Количество монет, которое есть у игрока.
        /// </summary>
        private static int _coins;

        /// <summary>
        /// Конструктор для инициализации количества монет.
        /// </summary>
        /// <param name="initialCoins">Начальное количество монет (по умолчанию 0).</param>
        public static void Init(int initialCoins = 0)
        {
            _coins = initialCoins;
        }

        /// <summary>
        /// Метод для добавления монет.
        /// </summary>
        /// <param name="amount">Количество монет для добавления.</param>
        public static void AddMoney(int amount)
        {
            if (amount > 0)
            {
                _coins += amount;
            }
        }

        /// <summary>
        /// Метод для траты монет.
        /// </summary>
        /// <param name="amount">Количество монет для траты.</param>
        /// <returns>Возвращает true, если удалось потратить монеты, иначе false.</returns>
        public static bool SpendMoney(int amount)
        {
            if (amount > 0 && _coins >= amount)
            {
                _coins -= amount;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Метод для проверки текущего баланса.
        /// </summary>
        /// <returns>Текущий баланс монет.</returns>
        public static int GetCurrentBalance()
        {
            return _coins;
        }

        // can spend money
        /// <summary>
        /// Метод для проверки возможности потратить монеты.
        /// </summary>
        /// <param name="amount">Количество монет для проверки.</param>
        /// <returns>Возвращает true, если можно потратить указанное количество монет, иначе false.</returns>
        /// <remarks>Метод не изменяет текущий баланс монет.</remarks>
        public static bool CanSpendMoney(int amount)
        {
            return amount > 0 && _coins >= amount;
        }

        public static void OnEnable()
        {
            GameEventsManager.OnCoinCollected += AddMoney;
        }

        public static void OnDisable()
        {
            GameEventsManager.OnCoinCollected -= AddMoney;
        }
    }
}