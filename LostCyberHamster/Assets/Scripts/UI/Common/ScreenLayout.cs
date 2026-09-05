using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace LostCyberHamster.UI
{
    /// <summary>Готовит первую геометрию экрана и поддерживает её при изменении viewport.</summary>
    public sealed class ScreenLayout : IDisposable
    {
        private readonly VisualElement _viewport;
        private readonly Action<Vector2> _apply;
        private readonly TaskCompletionSource<bool> _ready = new();
        private readonly IVisualElementScheduledItem _check;
        private Vector2 _appliedSize;
        private int _revision;
        private int _observedRevision = -1;
        private bool _disposed;

        public Task Ready => _ready.Task;

        public ScreenLayout(VisualElement viewport, Action<Vector2> apply = null)
        {
            _viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
            _apply = apply;
            _viewport.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            _check = _viewport.schedule.Execute(CheckLayout).Every(1);
        }

        /// <summary>Вписывает композицию в viewport, сохраняя исходные пропорции.</summary>
        public static ScreenLayout Fit(
            VisualElement viewport, VisualElement frame, VisualElement design,
            Vector2 designSize, VisualElement overlayFrame = null,
            VisualElement overlayDesign = null)
        {
            if (frame == null || design == null)
                throw new ArgumentException("Экран не содержит frame или design.");
            if (!IsValid(designSize))
                throw new ArgumentOutOfRangeException(nameof(designSize));

            return new ScreenLayout(viewport, size =>
            {
                float scale = Mathf.Min(size.x / designSize.x, size.y / designSize.y);
                ApplyScale(frame, design, designSize, scale);
                if (overlayFrame != null && overlayDesign != null)
                    ApplyScale(overlayFrame, overlayDesign, designSize, scale);
            });
        }

        private static void ApplyScale(
            VisualElement frame, VisualElement design, Vector2 size, float scale)
        {
            frame.style.width = size.x * scale;
            frame.style.height = size.y * scale;
            design.style.scale = new Scale(new Vector3(scale, scale, 1f));
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            Apply(evt.newRect.size);
        }

        private void Apply(Vector2 size)
        {
            if (_disposed || !IsValid(size) || size == _appliedSize)
                return;
            _apply?.Invoke(size);
            _appliedSize = size;
            _revision++;
        }

        private void CheckLayout()
        {
            // Получаем геометрию подключённого дерева.
            Vector2 size = _viewport.contentRect.size;
            if (_disposed || _viewport.panel == null || !IsValid(size))
                return;
            Apply(size);

            // Даём panel применить изменённые стили перед первым показом.
            if (_observedRevision != _revision)
            {
                _observedRevision = _revision;
                return;
            }
            _check.Pause();
            _ready.TrySetResult(true);
        }

        private static bool IsValid(Vector2 size)
        {
            return size.x > 0f && size.y > 0f &&
                   !float.IsInfinity(size.x) && !float.IsInfinity(size.y);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _viewport.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            _check.Pause();
            _ready.TrySetCanceled();
        }
    }
}
