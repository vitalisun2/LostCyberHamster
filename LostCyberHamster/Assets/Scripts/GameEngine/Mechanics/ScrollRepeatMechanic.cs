using UnityEngine;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public sealed class ScrollRepeatMechanic
    {
        private readonly Transform _transform;

        private readonly float _initPosX;
        private float _backgroundWidth;
        private SpriteRenderer _renderer;

        public ScrollRepeatMechanic(Transform transform)
        {
            _transform = transform;

            _initPosX = _transform.position.x;
            _renderer = _transform.GetComponentInChildren<SpriteRenderer>();
            RefreshBounds();
        }

        public void Update()
        {
            if (_backgroundWidth <= 0f)
            {
                RefreshBounds();
                if (_backgroundWidth <= 0f)
                {
                    return;
                }
            }

            if (_transform.position.x < _initPosX - _backgroundWidth)
            {
                _transform.position = new Vector3(_initPosX, _transform.position.y, _transform.position.z);
            }
        }

        // Recalculate cached width once the addressable sprite is ready; prevents zero-width scroll loops.
        public void RefreshBounds()
        {
            if (_renderer == null)
            {
                _renderer = _transform.GetComponentInChildren<SpriteRenderer>();
            }

            if (_renderer != null && _renderer.sprite != null)
            {
                _backgroundWidth = _renderer.bounds.size.x;
            }
        }
    }
}
