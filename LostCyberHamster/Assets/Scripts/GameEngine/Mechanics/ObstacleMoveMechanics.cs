using UnityEngine;

namespace Assets.Scripts.GameEngine.Mechanics
{
    /// <summary>
    /// Дополнительная механика движения препятствия влево (для walk анимации).
    /// Добавляется к базовому скроллу ScrollLeftMechanics.
    /// </summary>
    public sealed class ObstacleMoveMechanics
    {
        private readonly Transform _transform;
        
        // Дополнительная скорость движения (константа)
        private const float AdditionalSpeed = 0.5f;

        public ObstacleMoveMechanics(Transform transform)
        {
            _transform = transform;
        }

        public void Update(float deltaTime)
        {
            // Дополнительное движение влево (в ту же сторону, что и базовый скролл)
            float offset = AdditionalSpeed * deltaTime;
            float newX = _transform.position.x - offset;
            _transform.position = new Vector3(newX, _transform.position.y, _transform.position.z);
        }
    }
}
