namespace Vues.GameCore
{
    /// <summary>
    /// Хранилище кристаллов.
    /// </summary>
    public static class CrystalStorage
    {
        /// <summary>
        /// Количество кристаллов, которое есть у игрока.
        /// </summary>
        private static int _crystals;

        /// <summary>
        /// Конструктор для инициализации количества кристаллов.
        /// </summary>
        /// <param name="initialCrystals"></param>
        public static void Init(int initialCrystals = 0)
        {
            _crystals = initialCrystals;
        }

        /// <summary>
        /// Метод для добавления кристаллов.
        /// </summary>
        /// <param name="amount"></param>
        public static void AddCrystals(int amount)
        {
            if (amount > 0)
            {
                _crystals += amount;
            }
        }

        /// <summary>
        /// Метод для траты кристаллов.
        /// </summary>
        /// <param name="amount"></param>
        /// <returns></returns>
        public static bool SpendCrystals(int amount)
        {
            if (amount > 0 && _crystals >= amount)
            {
                _crystals -= amount;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Метод для проверки текущего баланса.
        /// </summary>
        /// <returns></returns>
        public static int GetCurrentBalance()
        {
            return _crystals;
        }

        /// <summary>
        /// Метод для проверки возможности потратить кристаллы.
        /// </summary>
        /// <param name="amount"></param>
        /// <returns></returns>
        public static bool CanSpendCrystals(int amount)
        {
            return amount > 0 && _crystals >= amount;
        }

        public static void OnEnable()
        {
            GameEventsManager.OnCrystalsCollected += AddCrystals;
        }

        public static void OnDisable()
        {
            GameEventsManager.OnCrystalsCollected -= AddCrystals;
        }
    }
}
