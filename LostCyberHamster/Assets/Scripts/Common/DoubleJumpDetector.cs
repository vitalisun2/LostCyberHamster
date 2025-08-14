using UnityEngine;

namespace Assets.Scripts.Common
{
    public class DoubleJumpDetector
    {
        private const float DoubleJumpThreshold = 0.3f;

        private float _lastJumpTime = -999f;
        private int _jumpCount;

        /// <summary>
        /// Регистрирует факт нажатия «прыжок».
        /// Возвращает true, если это было второе нажатие подряд (double jump).
        /// </summary>
        public bool RegisterJump()
        {
            var currentTime = Time.time;

            if (currentTime - _lastJumpTime <= DoubleJumpThreshold)
            {
                _jumpCount++;

                if (_jumpCount == 2)
                {
                    _jumpCount = 0;
                    _lastJumpTime = -999f;
                    return true;
                }
            }
            else
            {
                _jumpCount = 1;
            }

            _lastJumpTime = currentTime;
            return false;
        }

        public void Reset()
        {
            _lastJumpTime = -999f;
            _jumpCount = 0;
        }
    }

}
