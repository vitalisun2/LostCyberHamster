using System;
using System.Collections.Generic;
using Assets.Scripts.GameEngine.Skins;
using Assets.Scripts.Gameplay;
using UnityEngine;

namespace Vues.GameCore
{
    /// <summary>
    /// Управляет semantic visual-последовательностью Skateboard без изменения gameplay state.
    /// </summary>
    internal sealed class SkateboardVisualSequence
    {
        public const float PlaybackSpeed = 1.5f;

        private const float _collisionReactionVisualDuration =
            (8f / 12f) / PlaybackSpeed;
        private const float _run2SourceDuration = 8f / 12f;
        private const float _run3SourceDuration = 11f / 12f;

        private readonly SkinVisualHost _visualHost;
        private readonly float _jumpDuration;
        private readonly SkateboardRideAnimation[] _rideCycle;
        private readonly float _run2Speed;
        private readonly float _run3Speed;
        private readonly Dictionary<int, Obstacle> _reactedObstacles = new();

        private float _rideVisualTimeLeft;
        private long _nextActionId;
        private int _rideVisualIndex;
        private int _pendingCollisionReactions;
        private bool _isCollisionReactionPlaying;
        private bool _isPlaybackEnabled;

        public SkateboardVisualSequence(
            SkinVisualHost visualHost,
            float jumpDuration,
            IReadOnlyList<SkateboardRideAnimation> rideCycle,
            float run2Speed,
            float run3Speed)
        {
            _visualHost = visualHost ?? throw new ArgumentNullException(nameof(visualHost));
            _jumpDuration = jumpDuration;
            _rideCycle = CopyAndValidateCycle(rideCycle);
            _run2Speed = ValidateSpeed(run2Speed, nameof(run2Speed));
            _run3Speed = ValidateSpeed(run3Speed, nameof(run3Speed));
        }

        /// <summary>
        /// Очищает visual state нового mode lifecycle и перепривязывает Animator.
        /// </summary>
        public void Activate()
        {
            // Новый mode lifecycle не наследует ride/reaction state прошлой активации.
            ResetTransientState();
            ClearReactedObstacles();

            // Animator получает чистую binding и сразу возвращается в gameplay playback.
            _visualHost.Rebind();
            SetPlaybackEnabled(isEnabled: true);
        }

        /// <summary>
        /// Останавливает visual lifecycle и очищает реакции текущей активации.
        /// </summary>
        public void Deactivate()
        {
            // Завершённый mode освобождает все queued и obstacle-specific reactions.
            ResetTransientState();
            ClearReactedObstacles();
            SetPlaybackEnabled(isEnabled: false);
        }

        /// <summary>
        /// Приостанавливает или возобновляет Animator без продвижения visual timers.
        /// </summary>
        public void SetPlaybackEnabled(bool isEnabled)
        {
            if (_isPlaybackEnabled == isEnabled)
                return;

            _isPlaybackEnabled = isEnabled;
            _visualHost.SetPlaybackEnabled(isEnabled);
        }

        /// <summary>
        /// Продвигает prefab-configured ride cycle и очередь одноразовых Run 1 reactions.
        /// </summary>
        public void UpdateRide(float deltaTime)
        {
            _rideVisualTimeLeft -= deltaTime;

            // Сохраняем overflow кадра при переходах между one-shot и ride clips.
            while (_rideVisualTimeLeft <= 0f)
            {
                float overflow = -_rideVisualTimeLeft;
                if (_isCollisionReactionPlaying)
                {
                    if (_pendingCollisionReactions > 0)
                        PlayCollisionReaction();
                    else
                        RestartRide();
                }
                else
                {
                    PlayNextRideVisual();
                }

                _rideVisualTimeLeft -= overflow;
            }
        }

        /// <summary>
        /// Начинает основной ride loop заново с первого элемента prefab-конфигурации.
        /// </summary>
        public void RestartRide()
        {
            _isCollisionReactionPlaying = false;
            _rideVisualIndex = 0;
            PlayNextRideVisual();
        }

        /// <summary>
        /// Ставит одну Run 1 reaction для нового obstacle текущей активации.
        /// </summary>
        public void ReactToCollision(Obstacle obstacle)
        {
            if (obstacle == null)
                return;

            int instanceId = obstacle.GetInstanceID();
            if (_reactedObstacles.ContainsKey(instanceId))
                return;

            _reactedObstacles.Add(instanceId, obstacle);
            obstacle.OnObstacleUnspawned.Subscribe(OnObstacleUnspawned);

            _pendingCollisionReactions++;
            if (!_isCollisionReactionPlaying)
                PlayCollisionReaction();
        }

        /// <summary>
        /// Прерывает ride/reaction и резервирует общий ActionId нового jump-cycle.
        /// </summary>
        public long BeginJump()
        {
            _pendingCollisionReactions = 0;
            _isCollisionReactionPlaying = false;
            return ++_nextActionId;
        }

        /// <summary>
        /// Проигрывает normal или super вариант Skateboard Jump с заданным ActionId.
        /// </summary>
        public void PlayJump(bool isSuper, long actionId)
        {
            _visualHost.Play(new SkinActionContext(
                SkinVisualAction.SkateboardJump,
                isSuper ? SkinVisualVariant.Super : SkinVisualVariant.Normal,
                SkinVisualOutcome.Normal,
                _jumpDuration * PlaybackSpeed,
                actionId,
                PlaybackSpeed));
        }

        private void PlayNextRideVisual()
        {
            SkateboardRideAnimation animation = _rideCycle[_rideVisualIndex];
            _rideVisualIndex = (_rideVisualIndex + 1) % _rideCycle.Length;

            SkinVisualAction action;
            float sourceDuration;
            float speed;
            switch (animation)
            {
                case SkateboardRideAnimation.Run2:
                    action = SkinVisualAction.SkateboardRideA;
                    sourceDuration = _run2SourceDuration;
                    speed = _run2Speed;
                    break;
                case SkateboardRideAnimation.Run3:
                    action = SkinVisualAction.SkateboardRideB;
                    sourceDuration = _run3SourceDuration;
                    speed = _run3Speed;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(animation), animation, null);
            }

            _rideVisualTimeLeft = sourceDuration / speed;
            _visualHost.Play(new SkinActionContext(
                action,
                SkinVisualVariant.Normal,
                SkinVisualOutcome.Normal,
                sourceDuration,
                ++_nextActionId,
                speed));
        }

        private void PlayCollisionReaction()
        {
            _pendingCollisionReactions--;
            _isCollisionReactionPlaying = true;
            _rideVisualTimeLeft = _collisionReactionVisualDuration;
            _visualHost.Play(new SkinActionContext(
                SkinVisualAction.SkateboardCollisionReaction,
                SkinVisualVariant.Normal,
                SkinVisualOutcome.Normal,
                _collisionReactionVisualDuration * PlaybackSpeed,
                ++_nextActionId,
                PlaybackSpeed));
        }

        private void ResetTransientState()
        {
            _rideVisualTimeLeft = 0f;
            _rideVisualIndex = 0;
            _pendingCollisionReactions = 0;
            _isCollisionReactionPlaying = false;
        }

        private void ClearReactedObstacles()
        {
            foreach (Obstacle obstacle in _reactedObstacles.Values)
            {
                if (obstacle != null)
                    obstacle.OnObstacleUnspawned.Unsubscribe(OnObstacleUnspawned);
            }

            _reactedObstacles.Clear();
        }

        private void OnObstacleUnspawned(GameObject unspawnedObject)
        {
            int matchedInstanceId = 0;
            Obstacle matchedObstacle = null;
            foreach (KeyValuePair<int, Obstacle> entry in _reactedObstacles)
            {
                if (entry.Value != null && entry.Value.gameObject == unspawnedObject)
                {
                    matchedInstanceId = entry.Key;
                    matchedObstacle = entry.Value;
                    break;
                }
            }

            if (matchedObstacle == null)
                return;

            matchedObstacle.OnObstacleUnspawned.Unsubscribe(OnObstacleUnspawned);
            _reactedObstacles.Remove(matchedInstanceId);
        }

        private static SkateboardRideAnimation[] CopyAndValidateCycle(
            IReadOnlyList<SkateboardRideAnimation> rideCycle)
        {
            if (rideCycle == null)
                throw new ArgumentNullException(nameof(rideCycle));
            if (rideCycle.Count == 0)
                throw new ArgumentException("Skateboard ride cycle must not be empty.", nameof(rideCycle));

            var copy = new SkateboardRideAnimation[rideCycle.Count];
            for (int i = 0; i < rideCycle.Count; i++)
            {
                SkateboardRideAnimation animation = rideCycle[i];
                if (!Enum.IsDefined(typeof(SkateboardRideAnimation), animation))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(rideCycle),
                        animation,
                        "Skateboard ride cycle contains an unsupported animation.");
                }

                copy[i] = animation;
            }

            return copy;
        }

        private static float ValidateSpeed(float speed, string parameterName)
        {
            if (speed <= 0f || float.IsNaN(speed) || float.IsInfinity(speed))
                throw new ArgumentOutOfRangeException(parameterName, speed, "Playback speed must be positive.");

            return speed;
        }
    }
}
