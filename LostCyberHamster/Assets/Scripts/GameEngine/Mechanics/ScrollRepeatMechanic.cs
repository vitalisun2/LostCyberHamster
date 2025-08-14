using UnityEngine;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public sealed class ScrollRepeatMechanic
    {
        private readonly Transform _transform;

        readonly float _initPosX;
        readonly float _backgroundWidth;

        public ScrollRepeatMechanic(Transform transform)
        {
            _transform = transform;

            _initPosX = _transform.position.x;
            _backgroundWidth = _transform.GetComponentInChildren<SpriteRenderer>().bounds.size.x;
        }

        public void Update()
        {
            if (_transform.position.x < _initPosX - _backgroundWidth)
            {
                _transform.position = new Vector3(_initPosX, _transform.position.y, _transform.position.z);
            }
        }
    }
}
