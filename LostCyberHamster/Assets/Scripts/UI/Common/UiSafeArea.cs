using UnityEngine;
using UnityEngine.UIElements;

namespace LostCyberHamster.UI
{
    /// <summary>Переводит экранную safe area в локальные координаты UI Toolkit.</summary>
    internal static class UiSafeArea
    {
        public static Rect GetLocalRect(VisualElement viewport)
        {
            Rect available = viewport.contentRect;
            var panelRoot = viewport.panel?.visualTree;
            if (panelRoot == null || Screen.width <= 0 || Screen.height <= 0)
                return available;

            // Экранная система имеет начало снизу, panel — сверху.
            Rect panelBounds = panelRoot.worldBound;
            Rect safe = Screen.safeArea;
            Vector2 min = viewport.WorldToLocal(new Vector2(
                panelBounds.xMin + safe.xMin * panelBounds.width / Screen.width,
                panelBounds.yMin + (Screen.height - safe.yMax) * panelBounds.height / Screen.height));
            Vector2 max = viewport.WorldToLocal(new Vector2(
                panelBounds.xMin + safe.xMax * panelBounds.width / Screen.width,
                panelBounds.yMin + (Screen.height - safe.yMin) * panelBounds.height / Screen.height));

            // Пересечение сохраняет смещение при асимметричных вырезах.
            float xMin = Mathf.Clamp(min.x, available.xMin, available.xMax);
            float yMin = Mathf.Clamp(min.y, available.yMin, available.yMax);
            float xMax = Mathf.Clamp(max.x, xMin, available.xMax);
            float yMax = Mathf.Clamp(max.y, yMin, available.yMax);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }
    }
}
