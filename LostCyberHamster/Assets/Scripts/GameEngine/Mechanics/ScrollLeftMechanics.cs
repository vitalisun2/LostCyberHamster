using Atomic.Elements;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public sealed class ScrollLeftMechanics
    {
        private readonly Transform _transform;

        public ScrollLeftMechanics(Transform transform)
        {
            _transform = transform;
        }

        public void Update(float deltaTime)
        {
            float speed = Consts.BackgroundScrollSpeed * 3.8f;

            float newX = _transform.position.x - speed * deltaTime;
            _transform.position = new Vector3(newX, _transform.position.y, _transform.position.z);
        }
    }

}
