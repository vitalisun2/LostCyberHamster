using System;
using System.Collections.Generic;
using SystemRandom = System.Random;
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
        [SerializeField] private string _alternateRunStateName;
        [SerializeField, Min(1)] private int _minRunCyclesBeforeAlternate = 2;
        [SerializeField, Min(1)] private int _maxRunCyclesBeforeAlternate = 5;

        private int _activeStateHash;
        private long _activeActionId = -1;
        private bool _isDamaged;
        private bool _isPlaybackEnabled = true;
        private float _damageElapsed;
        private bool _isRunAlternationActive;
        private bool _isAlternateRunState;
        private int _runBaseStateHash;
        private int _runAlternateStateHash;
        private float _nextRunSwitchTime;
        private SystemRandom _runRandom;

        public IReadOnlyList<SkinVisualActionMapping> Mappings => _mappings;
        public IReadOnlyList<Sprite> PhysicsShapeSprites => _physicsShapeSprites;
        public SpriteRenderer SpriteRenderer => _spriteRenderer;

        private void Awake()
        {
            _animator ??= GetComponent<Animator>();
            _spriteRenderer ??= GetComponent<SpriteRenderer>();
            _runRandom = new SystemRandom(unchecked(Environment.TickCount ^ GetInstanceID()));
        }

        private void Update()
        {
            // Сохраняем damage feedback.
            if (_isDamaged && _isPlaybackEnabled && _spriteRenderer != null)
            {
                _damageElapsed += Time.deltaTime;
                _spriteRenderer.enabled = Mathf.FloorToInt(_damageElapsed * 12f) % 2 == 0;
            }

            if (!_isPlaybackEnabled || !_isRunAlternationActive || _animator == null)
                return;

            // Переключаем gait по завершении случайного числа циклов.
            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            int currentStateHash = _isAlternateRunState ? _runAlternateStateHash : _runBaseStateHash;
            if (stateInfo.fullPathHash != currentStateHash || stateInfo.normalizedTime < _nextRunSwitchTime)
                return;

            _isAlternateRunState = !_isAlternateRunState;
            int nextStateHash = _isAlternateRunState ? _runAlternateStateHash : _runBaseStateHash;
            _animator.Play(nextStateHash, 0, 0f);
            _activeStateHash = nextStateHash;
            _nextRunSwitchTime = RandomRunCyclesBeforeAlternate();
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

            // Вычисляем FitToAction и сохраняем фазу при normal-to-super upgrade.
            string statePath = $"{_animator.GetLayerName(0)}.{mapping.StateName}";
            int stateHash = Animator.StringToHash(statePath);
            bool continuesSameAction = context.ActionId == _activeActionId;

            // Включаем alternation только для настроенного loop-run state.
            bool alternatesRun = CanAlternateRun(context, mapping, statePath, out int alternateStateHash);
            bool startsAlternatingRun = false;
            if (alternatesRun)
            {
                bool continuesAlternatingRun = _isRunAlternationActive
                                               && continuesSameAction
                                               && _runBaseStateHash == stateHash
                                               && _runAlternateStateHash == alternateStateHash;
                startsAlternatingRun = !continuesAlternatingRun;
                if (!continuesAlternatingRun)
                {
                    _isAlternateRunState = false;
                    _runBaseStateHash = stateHash;
                    _runAlternateStateHash = alternateStateHash;
                }

                _isRunAlternationActive = true;
                stateHash = _isAlternateRunState ? _runAlternateStateHash : _runBaseStateHash;
            }
            else
            {
                _isRunAlternationActive = false;
                _isAlternateRunState = false;
            }

            float normalizedTime = continuesSameAction
                ? Mathf.Clamp01(_animator.GetCurrentAnimatorStateInfo(0).normalizedTime)
                : 0f;
            if (alternatesRun && startsAlternatingRun)
                normalizedTime = 0f;
            float speed = CalculateSpeed(mapping, context, continuesSameAction);
            _animator.SetFloat(SpeedParameterName, speed);

            if (!continuesSameAction || stateHash != _activeStateHash || startsAlternatingRun)
                _animator.Play(stateHash, 0, normalizedTime);

            if (alternatesRun && startsAlternatingRun)
                _nextRunSwitchTime = RandomRunCyclesBeforeAlternate();

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
            _isRunAlternationActive = false;
            _isAlternateRunState = false;
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

        private bool CanAlternateRun(
            in SkinActionContext context,
            SkinVisualActionMapping mapping,
            string baseStatePath,
            out int alternateStateHash)
        {
            alternateStateHash = 0;
            if (_animator == null
                || !mapping.Loop
                || context.Action is not (SkinVisualAction.GroundRun or SkinVisualAction.RoofRun)
                || string.IsNullOrWhiteSpace(_alternateRunStateName))
            {
                return false;
            }

            string alternateStatePath = $"{_animator.GetLayerName(0)}.{_alternateRunStateName}";
            alternateStateHash = Animator.StringToHash(alternateStatePath);
            return alternateStateHash != Animator.StringToHash(baseStatePath)
                   && _animator.HasState(0, Animator.StringToHash(baseStatePath))
                   && _animator.HasState(0, alternateStateHash);
        }

        private int RandomRunCyclesBeforeAlternate()
        {
            int minimum = Mathf.Max(1, _minRunCyclesBeforeAlternate);
            int maximum = Mathf.Max(minimum, _maxRunCyclesBeforeAlternate);
            return _runRandom.Next(minimum, maximum + 1);
        }
    }
}
