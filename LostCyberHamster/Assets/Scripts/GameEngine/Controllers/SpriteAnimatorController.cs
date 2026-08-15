using Assets.Scripts.GameEngine.Skins;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Controllers
{
    /// <summary>
    /// Постоянный gameplay-facing фасад семантических сигналов активному SkinVisual.
    /// </summary>
    public sealed class SpriteAnimatorController : MonoBehaviour,
        Listeners.IGamePauseListener,
        Listeners.IGameResumeListener,
        Listeners.IGameFinishListener,
        Listeners.IGameIntroListener,
        Listeners.IGameStartListener
    {
        [SerializeField] private SkinVisualHost _visualHost;
        [SerializeField] private TransformAnimatorController _transformAnimatorController;

        private SkinActionContext? _lastContext;
        private long _nextActionId;

        private void Awake()
        {
            Hamster hamster = GetComponentInParent<Hamster>();
            _visualHost ??= hamster.GetComponentInChildren<SkinVisualHost>(true);
            _transformAnimatorController ??= hamster.GetComponentInChildren<TransformAnimatorController>(true);
        }

        /// <summary>
        /// Преобразует текущее gameplay-состояние в visual context и запускает его через host.
        /// </summary>
        public void PlayForState(HamsterStateEnum state)
        {
            // Разрешаем action contract и authoritative transform duration.
            if (!SkinActionResolver.TryResolve(state, out SkinActionDescriptor descriptor))
            {
                Debug.LogWarning($"No SkinVisual action for hamster state '{state}'.", this);
                return;
            }

            // Normal-to-super одного action сохраняет ActionId и продолжает совместимый state.
            float duration = ResolveDuration(descriptor.TransformClipName);
            long actionId = IsUpgradeOfCurrentAction(descriptor)
                ? _lastContext.Value.ActionId
                : ++_nextActionId;
            var context = new SkinActionContext(
                descriptor.Action,
                descriptor.Variant,
                descriptor.Outcome,
                duration,
                actionId);

            _visualHost.Play(context);
            _lastContext = context;
        }

        public void SetDamaged(bool isDamaged)
        {
            _visualHost.SetDamaged(isDamaged);
        }

        public void OnPause()
        {
            _visualHost.SetPlaybackEnabled(false);
        }

        public void OnResume()
        {
            _visualHost.SetPlaybackEnabled(true);
        }

        public void OnIntro()
        {
            _visualHost.SetPlaybackEnabled(false);
        }

        public void OnStart()
        {
            _lastContext = null;
            _visualHost.Rebind();
            _visualHost.SetPlaybackEnabled(true);
            PlayForState(HamsterStateEnum.Run);
        }

        public void OnFinish()
        {
            _visualHost.SetPlaybackEnabled(false);
        }

        private float ResolveDuration(string transformClipName)
        {
            if (_transformAnimatorController.TryFindClip(transformClipName, out AnimationClip clip)
                && clip != null)
            {
                return clip.length;
            }

            Debug.LogError($"Transform clip '{transformClipName}' is missing for SkinVisual timing.", this);
            return 1f;
        }

        private bool IsUpgradeOfCurrentAction(in SkinActionDescriptor descriptor)
        {
            if (!_lastContext.HasValue)
                return false;

            SkinActionContext previous = _lastContext.Value;
            return !previous.IsLoop
                   && previous.Action == descriptor.Action
                   && previous.Variant == SkinVisualVariant.Normal
                   && descriptor.Variant == SkinVisualVariant.Super;
        }
    }
}
