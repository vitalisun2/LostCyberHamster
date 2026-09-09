using System;
using Assets.Scripts.Gameplay;

namespace Vues.GameCore
{
    /// <summary>
    /// Описывает суперудар, готовый к применению в забеге.
    /// </summary>
    public interface ISuperAttackRuntime : IDisposable
    {
        /// <summary>
        /// Возвращает заряд за одно уничтоженное препятствие.
        /// </summary>
        int ChargePerObstacle { get; }
        SuperAttackRuntimeSnapshot Snapshot { get; }

        /// <summary>Принимает подтверждённое разрушение до возврата цели в пул.</summary>
        void OnObstacleDestroyed(Obstacle obstacle);

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
