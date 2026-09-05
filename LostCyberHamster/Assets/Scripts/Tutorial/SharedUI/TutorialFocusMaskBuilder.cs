using System;
using UnityEngine;

namespace Assets.Scripts.Tutorial
{
    /// <summary>
    /// Строит focus mask и владеет созданной runtime-текстурой.
    /// </summary>
    public sealed class TutorialFocusMaskBuilder : IDisposable
    {
        private const float _roundedRectCornerRadius = 28f;

        private Texture2D _texture;
        private Color32[] _pixels;
        private bool _isDisposed;

        /// <summary>
        /// Создаёт новую маску и уничтожает ранее созданную текстуру.
        /// </summary>
        public Texture2D Build(
            Rect focusRect,
            TutorialFocusShape shape,
            Rect rootRect,
            float dimAlpha,
            float softFocusWidth,
            int maxWidth)
        {
            ThrowIfDisposed();
            if (rootRect.width <= 0f || rootRect.height <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(rootRect), "Размер области маски должен быть положительным.");
            }

            // Строим пиксели в пониженном разрешении для дешёвого мягкого края.
            int safeMaxWidth = Mathf.Max(128, maxWidth);
            int maskWidth = Mathf.Clamp(Mathf.RoundToInt(rootRect.width / 2f), 128, safeMaxWidth);
            int maskHeight = Mathf.Max(1, Mathf.RoundToInt(maskWidth * rootRect.height / rootRect.width));
            float safeSoftFocusWidth = Mathf.Max(1f, softFocusWidth);
            float safeDimAlpha = Mathf.Clamp01(dimAlpha);
            EnsureTexture(maskWidth, maskHeight);

            for (int y = 0; y < maskHeight; y++)
            {
                float rootY = rootRect.yMax - (y + 0.5f) / maskHeight * rootRect.height;
                for (int x = 0; x < maskWidth; x++)
                {
                    float rootX = rootRect.xMin + (x + 0.5f) / maskWidth * rootRect.width;
                    float distance = GetDistanceToFocus(
                        new Vector2(rootX, rootY),
                        focusRect,
                        rootRect,
                        shape);
                    byte alpha = (byte)Mathf.RoundToInt(
                        Mathf.Clamp01(distance / safeSoftFocusWidth) * safeDimAlpha * 255f);
                    _pixels[y * maskWidth + x] = new Color32(0, 0, 0, alpha);
                }
            }

            _texture.SetPixels32(_pixels);
            _texture.Apply(false, false);
            return _texture;
        }

        /// <summary>
        /// Уничтожает текущую runtime-текстуру маски.
        /// </summary>
        public void Clear()
        {
            if (_isDisposed)
            {
                return;
            }

            ReleaseTexture();
        }

        /// <summary>
        /// Освобождает принадлежащую builder runtime-текстуру.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            ReleaseTexture();
            _isDisposed = true;
        }

        /// <summary>Проверяет принадлежность точки той же форме, из которой строится маска.</summary>
        public static bool ContainsFocusPoint(
            Vector2 point,
            Rect focusRect,
            Rect rootRect,
            TutorialFocusShape shape)
        {
            return rootRect.Contains(point)
                   && focusRect.width > 0f
                   && focusRect.height > 0f
                   && GetDistanceToFocus(point, focusRect, rootRect, shape) <= 0f;
        }

        private static float GetDistanceToFocus(
            Vector2 point,
            Rect focusRect,
            Rect rootRect,
            TutorialFocusShape shape)
        {
            if (shape == TutorialFocusShape.Circle)
            {
                float circleRadius = Mathf.Min(focusRect.width, focusRect.height) * 0.5f;
                return Vector2.Distance(point, focusRect.center) - circleRadius;
            }

            Rect adjustedRect = AdjustRoundedRectForScreenEdges(
                focusRect,
                rootRect,
                _roundedRectCornerRadius);
            return GetRoundedRectDistance(point, adjustedRect, _roundedRectCornerRadius);
        }

        private static Rect AdjustRoundedRectForScreenEdges(Rect rect, Rect rootRect, float radius)
        {
            float xMin = rect.xMin <= rootRect.xMin ? rect.xMin - radius : rect.xMin;
            float yMin = rect.yMin <= rootRect.yMin ? rect.yMin - radius : rect.yMin;
            float xMax = rect.xMax >= rootRect.xMax ? rect.xMax + radius : rect.xMax;
            float yMax = rect.yMax >= rootRect.yMax ? rect.yMax + radius : rect.yMax;
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static float GetRoundedRectDistance(Vector2 point, Rect rect, float radius)
        {
            float safeRadius = Mathf.Min(radius, Mathf.Min(rect.width, rect.height) * 0.5f);
            Vector2 halfSize = new Vector2(rect.width, rect.height) * 0.5f;
            Vector2 distanceFromEdge = new Vector2(
                Mathf.Abs(point.x - rect.center.x),
                Mathf.Abs(point.y - rect.center.y)) - (halfSize - Vector2.one * safeRadius);
            Vector2 outside = new Vector2(
                Mathf.Max(distanceFromEdge.x, 0f),
                Mathf.Max(distanceFromEdge.y, 0f));
            float inside = Mathf.Min(Mathf.Max(distanceFromEdge.x, distanceFromEdge.y), 0f);
            return outside.magnitude + inside - safeRadius;
        }

        private void ReleaseTexture()
        {
            if (_texture == null)
            {
                return;
            }

            UnityEngine.Object.Destroy(_texture);
            _texture = null;
            _pixels = null;
        }

        private void EnsureTexture(int width, int height)
        {
            if (_texture != null && _texture.width == width && _texture.height == height)
            {
                return;
            }

            ReleaseTexture();
            _pixels = new Color32[width * height];
            _texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Tutorial Focus Mask",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(TutorialFocusMaskBuilder));
            }
        }
    }
}
