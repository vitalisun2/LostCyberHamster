using UnityEngine;

namespace Assets.Scripts.Tutorial
{
    public static class TutorialFocusMaskBuilder
    {
        private const float _roundedRectCornerRadius = 28f;

        public static Texture2D Create(
            Rect focusRect,
            TutorialFocusShape shape,
            Rect rootRect,
            float dimAlpha,
            float softFocusWidth,
            int maxWidth)
        {
            int maskWidth = Mathf.Clamp(Mathf.RoundToInt(rootRect.width / 2f), 128, maxWidth);
            int maskHeight = Mathf.Max(1, Mathf.RoundToInt(maskWidth * rootRect.height / rootRect.width));
            var pixels = new Color32[maskWidth * maskHeight];

            for (int y = 0; y < maskHeight; y++)
            {
                float rootY = rootRect.height - (y + 0.5f) / maskHeight * rootRect.height;
                for (int x = 0; x < maskWidth; x++)
                {
                    float rootX = (x + 0.5f) / maskWidth * rootRect.width;
                    float distance = GetDistanceToFocus(
                        new Vector2(rootX, rootY),
                        focusRect,
                        rootRect,
                        shape);
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(distance / softFocusWidth) * dimAlpha * 255f);
                    pixels[y * maskWidth + x] = new Color32(0, 0, 0, alpha);
                }
            }

            var texture = new Texture2D(maskWidth, maskHeight, TextureFormat.RGBA32, false)
            {
                name = "Tutorial Focus Mask",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
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

            Rect adjustedRect = AdjustRoundedRectForScreenEdges(focusRect, rootRect, _roundedRectCornerRadius);
            return GetRoundedRectDistance(point, adjustedRect, _roundedRectCornerRadius);
        }

        private static Rect AdjustRoundedRectForScreenEdges(Rect rect, Rect rootRect, float radius)
        {
            float xMin = rect.xMin <= 0f ? rect.xMin - radius : rect.xMin;
            float yMin = rect.yMin <= 0f ? rect.yMin - radius : rect.yMin;
            float xMax = rect.xMax >= rootRect.width ? rect.xMax + radius : rect.xMax;
            float yMax = rect.yMax >= rootRect.height ? rect.yMax + radius : rect.yMax;
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static float GetRoundedRectDistance(Vector2 point, Rect rect, float radius)
        {
            float safeRadius = Mathf.Min(radius, Mathf.Min(rect.width, rect.height) * 0.5f);
            Vector2 halfSize = new Vector2(rect.width, rect.height) * 0.5f;
            Vector2 center = rect.center;
            Vector2 q = new Vector2(
                Mathf.Abs(point.x - center.x),
                Mathf.Abs(point.y - center.y)) - (halfSize - Vector2.one * safeRadius);

            Vector2 outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
            float inside = Mathf.Min(Mathf.Max(q.x, q.y), 0f);
            return outside.magnitude + inside - safeRadius;
        }
    }
}
