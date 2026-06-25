using Atomic.Elements;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public sealed class ScrollLeftMechanics
    {
        public const float SpeedMultiplier = 3.8f;

        private readonly Transform _transform;
        private readonly float _scrollSpeed;

        public ScrollLeftMechanics(Transform transform, float scrollSpeed = Consts.BackgroundScrollSpeed)
        {
            _transform = transform;
            _scrollSpeed = scrollSpeed;
        }

        public void Update(float deltaTime)
        {
            float speed = _scrollSpeed * SpeedMultiplier;

            float newX = _transform.position.x - speed * deltaTime;
            _transform.position = new Vector3(newX, _transform.position.y, _transform.position.z);
        }
    }

}
