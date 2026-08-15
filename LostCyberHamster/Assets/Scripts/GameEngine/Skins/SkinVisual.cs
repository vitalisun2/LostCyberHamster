using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Skins
{
    /// <summary>
    /// Управляет Animator и SpriteRenderer одного prefab-визуала скина.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkinVisual : MonoBehaviour
    {
        public const string SpeedParameterName = "VisualSpeed";

        [SerializeField] private Animator _animator;
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private List<SkinVisualActionMapping> _mappings = new();
        [SerializeField] private List<Sprite> _physicsShapeSprites = new();

        private int _activeStateHash;
        private long _activeActionId = -1;
        private bool _isDamaged;
        private bool _isPlaybackEnabled = true;
        private float _damageElapsed;

        public IReadOnlyList<SkinVisualActionMapping> Mappings => _mappings;
        public IReadOnlyList<Sprite> PhysicsShapeSprites => _physicsShapeSprites;
        public SpriteRenderer SpriteRenderer => _spriteRenderer;

        private void Awake()
        {
            _animator ??= GetComponent<Animator>();
            _spriteRenderer ??= GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            if (!_isDamaged || !_isPlaybackEnabled || _spriteRenderer == null)
                return;

            _damageElapsed += Time.deltaTime;
            _spriteRenderer.enabled = Mathf.FloorToInt(_damageElapsed * 12f) % 2 == 0;
        }

        private void OnDisable()
        {
            if (_spriteRenderer != null)
                _spriteRenderer.enabled = true;
        }

        /// <summary>
        /// Запускает выбранный visual state и подгоняет one-shot клип под длительность transform-action.
        /// </summary>
        public void Play(in SkinActionContext context)
        {
            // Выбираем самое специфичное правило mapping.
            SkinVisualActionMapping mapping = Resolve(context);
            if (mapping == null)
            {
                Debug.LogError($"SkinVisual '{name}' has no mapping for {context.Action}/{context.Variant}/{context.Outcome}.", this);
                return;
            }

            // Вычисляем FitToAction и не перезапускаем тот же state при normal-to-super upgrade.
            string statePath = $"{_animator.GetLayerName(0)}.{mapping.StateName}";
            int stateHash = Animator.StringToHash(statePath);
            bool continuesSameAction = stateHash == _activeStateHash && context.ActionId == _activeActionId;
            float speed = CalculateSpeed(mapping, context, continuesSameAction);
            _animator.SetFloat(SpeedParameterName, speed);

            if (!continuesSameAction)
                _animator.Play(stateHash, 0, 0f);

            _activeStateHash = stateHash;
            _activeActionId = context.ActionId;
        }

        /// <summary>
        /// Включает или выключает косметический damage feedback.
        /// </summary>
        public void SetDamaged(bool isDamaged)
        {
            _isDamaged = isDamaged;
            _damageElapsed = 0f;
            if (!isDamaged && _spriteRenderer != null)
                _spriteRenderer.enabled = true;
        }

        /// <summary>
        /// Приостанавливает или возобновляет visual Animator без влияния на gameplay.
        /// </summary>
        public void SetPlaybackEnabled(bool isEnabled)
        {
            _isPlaybackEnabled = isEnabled;
            if (_animator != null)
                _animator.enabled = isEnabled;
        }

        /// <summary>
        /// Возвращает visual Animator в исходное состояние перед стартом забега.
        /// </summary>
        public void Rebind()
        {
            _activeStateHash = 0;
            _activeActionId = -1;
            _animator.Rebind();
        }

#if UNITY_EDITOR
        public void ConfigureEditor(
            Animator animator,
            SpriteRenderer spriteRenderer,
            List<SkinVisualActionMapping> mappings,
            List<Sprite> physicsShapeSprites = null)
        {
            _animator = animator;
            _spriteRenderer = spriteRenderer;
            _mappings = mappings;
            if (physicsShapeSprites != null)
                _physicsShapeSprites = physicsShapeSprites;
        }
#endif

        private SkinVisualActionMapping Resolve(in SkinActionContext context)
        {
            SkinVisualActionMapping result = null;
            int bestSpecificity = -1;
            for (int index = 0; index < _mappings.Count; index++)
            {
                SkinVisualActionMapping candidate = _mappings[index];
                if (candidate == null || !candidate.Matches(context))
                    continue;

                if (candidate.Specificity <= bestSpecificity)
                    continue;

                result = candidate;
                bestSpecificity = candidate.Specificity;
            }

            return result;
        }

        private float CalculateSpeed(
            SkinVisualActionMapping mapping,
            in SkinActionContext context,
            bool continuesSameAction)
        {
            float playbackSpeed = Mathf.Max(0.01f, context.PlaybackSpeed);
            if (mapping.Loop || context.IsLoop || mapping.Clip == null || context.Duration <= 0f)
                return playbackSpeed;

            float remainingNormalized = 1f;
            if (continuesSameAction)
            {
                AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
                remainingNormalized = Mathf.Clamp01(1f - stateInfo.normalizedTime);
            }

            float fitToAction = mapping.Clip.length * remainingNormalized / context.Duration;
            return Mathf.Max(0.01f, fitToAction * playbackSpeed);
        }
    }
}
