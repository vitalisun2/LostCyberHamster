using System;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Skins
{
    /// <summary>
    /// Постоянный Hamster-owned слот, который делегирует visual-сигналы загруженному prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkinVisualHost : MonoBehaviour
    {
        private SkinVisual _visual;
        private SkinActionContext? _pendingContext;
        private bool _isDamaged;
        private bool _isPlaybackEnabled = true;

        public Transform Slot => transform;

        /// <summary>
        /// Подключает единственный visual и восстанавливает накопленное состояние host.
        /// </summary>
        public void Bind(SkinVisual visual)
        {
            if (visual == null)
                throw new MissingComponentException("Skin visual prefab must contain SkinVisual.");

            if (_visual != null)
                throw new InvalidOperationException("SkinVisualHost already has an active visual.");

            _visual = visual;
            _visual.SetPlaybackEnabled(_isPlaybackEnabled);
            _visual.SetDamaged(_isDamaged);
            if (_pendingContext.HasValue)
                _visual.Play(_pendingContext.Value);
        }

        /// <summary>
        /// Отключает текущий visual при освобождении runtime lease.
        /// </summary>
        public void Unbind(SkinVisual visual)
        {
            if (_visual == visual)
                _visual = null;
        }

        /// <summary>
        /// Запоминает действие и передаёт его активному visual.
        /// </summary>
        public void Play(in SkinActionContext context)
        {
            _pendingContext = context;
            _visual?.Play(context);
        }

        public void SetDamaged(bool isDamaged)
        {
            _isDamaged = isDamaged;
            _visual?.SetDamaged(isDamaged);
        }

        public void SetPlaybackEnabled(bool isEnabled)
        {
            _isPlaybackEnabled = isEnabled;
            _visual?.SetPlaybackEnabled(isEnabled);
        }

        public void Rebind()
        {
            _visual?.Rebind();
        }
    }
}
