namespace Vues.GameCore
{
    /// <summary>
    /// Описывает суперудар, готовый к применению в забеге.
    /// </summary>
    public interface ISuperAttackRuntime
    {
        /// <summary>
        /// Возвращает заряд за одно уничтоженное препятствие.
        /// </summary>
        int ChargePerObstacle { get; }

        /// <summary>
        /// Пытается применить суперудар.
        /// </summary>
        bool TryActivate();

        /// <summary>
        /// Обновляет состояние суперудара.
        /// </summary>
        void Update();
    }
}
