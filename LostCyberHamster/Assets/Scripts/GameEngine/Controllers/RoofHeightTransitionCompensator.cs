using Assets.Scripts;
using Assets.Scripts.Common.Models;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Controllers
{
    /// <summary>
    /// Компенсирует разницу высоты roof-run клипов при Big↔Medium прыжках с крыши на крышу.
    /// </summary>
    internal sealed class RoofHeightTransitionCompensator
    {
        /// <summary>
        /// Transform, к которому применяется временная компенсация высоты.
        /// </summary>
        private readonly Transform _targetTransform;

        /// <summary>
        /// Признак активной компенсации высоты.
        /// </summary>
        private bool _isActive;

        /// <summary>
        /// Начальное смещение по Y, которое плавно сводится к нулю.
        /// </summary>
        private float _startOffsetY;

        /// <summary>
        /// Длительность компенсации в секундах.
        /// </summary>
        private float _duration;

        /// <summary>
        /// Время, прошедшее с начала компенсации.
        /// </summary>
        private float _elapsed;

        /// <summary>
        /// Возвращает true, если сейчас идёт компенсация высоты.
        /// </summary>
        internal bool IsActive => _isActive;

        internal RoofHeightTransitionCompensator(Transform targetTransform)
        {
            _targetTransform = targetTransform;
        }

        /// <summary>
        /// Запускает компенсацию для Big↔Medium roof-to-roof перехода.
        /// </summary>
        internal bool TryStart(
            ObstacleTypeEnum sourceRoofType,
            ObstacleTypeEnum targetRoofType,
            float duration)
        {
            // Проверяет поддерживаемый roof-to-roof переход.
            if (!IsCrossHeightRoofTransition(sourceRoofType, targetRoofType))
                return false;

            // Проверяет корректность времени компенсации.
            if (duration <= 0f)
                return false;

            // Рассчитывает параметры компенсации.
            if (!TryGetTransition(sourceRoofType, targetRoofType, out float offsetY))
                return false;

            // Запускает активную компенсацию.
            Start(offsetY, duration);
            return true;
        }

        /// <summary>
        /// Применяет текущий кадр компенсации поверх позиции, выставленной Animator-ом.
        /// </summary>
        internal void ApplyFrame(float deltaTime)
        {
            // Пропускает кадр без активной компенсации.
            if (!_isActive)
                return;

            // Рассчитывает текущее сглаженное смещение.
            _elapsed += deltaTime;
            float progress = Mathf.Clamp01(_elapsed / _duration);
            float offsetY = Mathf.SmoothStep(_startOffsetY, 0f, progress);

            // Накладывает смещение поверх позиции Animator-а.
            Vector3 localPosition = _targetTransform.localPosition;
            localPosition.y += offsetY;
            _targetTransform.localPosition = localPosition;

            // Завершает компенсацию в конце интервала.
            if (progress >= 1f)
                _isActive = false;
        }

        /// <summary>
        /// Сбрасывает активную компенсацию высоты.
        /// </summary>
        internal void Reset()
        {
            _isActive = false;
            _startOffsetY = 0f;
            _duration = 0f;
            _elapsed = 0f;
        }

        /// <summary>
        /// Проверяет, является ли переход roof-to-roof сменой высоты Big↔Medium.
        /// </summary>
        private static bool IsCrossHeightRoofTransition(
            ObstacleTypeEnum sourceRoofType,
            ObstacleTypeEnum targetRoofType)
        {
            // Проверяет переход с большой крыши на среднюю.
            if (sourceRoofType == ObstacleTypeEnum.bigNotAlive &&
                targetRoofType == ObstacleTypeEnum.mediumNotAlive)
            {
                return true;
            }

            // Проверяет переход со средней крыши на большую.
            if (sourceRoofType == ObstacleTypeEnum.mediumNotAlive &&
                targetRoofType == ObstacleTypeEnum.bigNotAlive)
            {
                return true;
            }

            // Остальные переходы не требуют компенсации.
            return false;
        }

        /// <summary>
        /// Рассчитывает начальное Y-смещение компенсации.
        /// </summary>
        private bool TryGetTransition(
            ObstacleTypeEnum sourceRoofType,
            ObstacleTypeEnum targetRoofType,
            out float offsetY)
        {
            // Инициализирует выходные значения для неуспешных веток.
            offsetY = 0f;

            // Получает стабильные высоты roof-run клипов.
            if (!TryGetRoofRunStartY(sourceRoofType, out float sourceRoofY) ||
                !TryGetRoofRunStartY(targetRoofType, out float targetRoofY))
            {
                return false;
            }

            // Возвращает ненулевую разницу высот.
            offsetY = sourceRoofY - targetRoofY;
            return !Mathf.Approximately(offsetY, 0f);
        }

        /// <summary>
        /// Запоминает параметры компенсации и переводит её в активное состояние.
        /// </summary>
        private void Start(float startOffsetY, float duration)
        {
            _startOffsetY = startOffsetY;
            _duration = duration;
            _elapsed = 0f;
            _isActive = true;
        }

        /// <summary>
        /// Возвращает начальную Y-позицию roof-run клипа для указанного типа крыши.
        /// </summary>
        private static bool TryGetRoofRunStartY(ObstacleTypeEnum roofType, out float roofRunY)
        {
            // Возвращает высоту большого roof-run клипа.
            if (roofType == ObstacleTypeEnum.bigNotAlive)
            {
                roofRunY = Consts.BIG_ROOF_RUN_START_Y;
                return true;
            }

            // Возвращает высоту medium roof-run клипа.
            if (roofType == ObstacleTypeEnum.mediumNotAlive)
            {
                roofRunY = Consts.MEDIUM_ROOF_RUN_START_Y;
                return true;
            }

            // Не поддерживает остальные типы препятствий.
            roofRunY = 0f;
            return false;
        }
    }
}
