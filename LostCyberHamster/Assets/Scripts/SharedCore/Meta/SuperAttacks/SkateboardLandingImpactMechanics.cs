using System;
using System.Collections.Generic;
using Assets.Scripts;
using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
using UnityEngine;

namespace Vues.GameCore
{
    /// <summary>
    /// Подбрасывает видимые препятствия при skateboard landing и уничтожает их с задержкой.
    /// </summary>
    public sealed class SkateboardLandingImpactMechanics :
        Listeners.IGameUpdateListener,
        Listeners.IGamePauseListener,
        Listeners.IGameResumeListener,
        Listeners.IGameFinishListener,
        IDisposable
    {
        public const float DefaultBumpHeightFraction = 0.1f;
        public const float DefaultBumpDuration = 5f / 60f;
        public const float DefaultDestroyDelay = 3f / 60f;
        public const float DefaultComboThreeWaveDuration = 0.26f;

        /// <summary>
        /// Неизменяемый вход одной landing wave из authoritative jump-cycle.
        /// </summary>
        public readonly struct ImpactRequest
        {
            public ImpactRequest(
                long actionId,
                int comboDepth,
                bool isSuperCycle,
                bool startedOnRoof,
                Obstacle currentSupport,
                Obstacle missedRoof)
            {
                ActionId = actionId;
                ComboDepth = comboDepth;
                IsSuperCycle = isSuperCycle;
                StartedOnRoof = startedOnRoof;
                CurrentSupport = currentSupport;
                MissedRoof = missedRoof;
            }

            public long ActionId { get; }
            public int ComboDepth { get; }
            public bool IsSuperCycle { get; }
            public bool StartedOnRoof { get; }
            public Obstacle CurrentSupport { get; }
            public Obstacle MissedRoof { get; }
        }

        private const float _bumpHeightFeedbackMultiplier = 1.3f;
        private const float _comboOneWaveDuration = 0.08f;
        private const float _comboOneMinimumFalloff = 0.7f;
        private const float _comboTwoRadiusInHamsterWidths = 3f;
        private const float _comboTwoWaveDuration = 0.16f;
        private const float _comboTwoStrength = 1.5f;
        private const float _comboTwoMinimumFalloff = 0.55f;
        private const float _comboThreeStrength = 2.1f;
        private const float _comboThreeMinimumFalloff = 0.45f;
        private const float _superJumpRadiusMultiplier = 2f;
        private const float _maximumStartJitter = 0.012f;
        private const int _waveGroupCount = 3;
        private const int _maximumComboDepth = 3;

        private readonly Hamster _hamster;
        private readonly ObstacleSpawner _obstacleSpawner;
        private readonly GameManager _gameManager;
        private readonly Camera _camera;
        private readonly ICameraShake _cameraShake;
        private readonly float _bumpHeightFraction;
        private readonly float _bumpDuration;
        private readonly float _destroyDelay;
        private readonly float _comboThreeWaveDuration;
        private readonly List<ImpactTarget> _targets = new(32);
        private readonly System.Random _startJitterRandom = new();

        private bool _isDisposed;

        /// <summary>
        /// Подключает mechanics к gameplay update loop с явными runtime dependencies.
        /// </summary>
        public SkateboardLandingImpactMechanics(
            Hamster hamster,
            ObstacleSpawner obstacleSpawner,
            GameManager gameManager,
            Camera camera,
            ICameraShake cameraShake,
            float bumpHeightFraction = DefaultBumpHeightFraction,
            float bumpDuration = DefaultBumpDuration,
            float destroyDelay = DefaultDestroyDelay,
            float comboThreeWaveDuration = DefaultComboThreeWaveDuration)
        {
            _hamster = hamster ?? throw new ArgumentNullException(nameof(hamster));
            _obstacleSpawner = obstacleSpawner ??
                throw new ArgumentNullException(nameof(obstacleSpawner));
            _gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
            _cameraShake = cameraShake ??
                throw new ArgumentNullException(nameof(cameraShake));

            if (bumpHeightFraction <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bumpHeightFraction),
                    bumpHeightFraction,
                    "Bump height fraction must be positive.");
            }

            if (bumpDuration <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bumpDuration),
                    bumpDuration,
                    "Bump duration must be positive.");
            }

            if (destroyDelay < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(destroyDelay),
                    destroyDelay,
                    "Destroy delay must not be negative.");
            }

            if (comboThreeWaveDuration < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(comboThreeWaveDuration),
                    comboThreeWaveDuration,
                    "Combo-three wave duration must not be negative.");
            }

            _bumpHeightFraction = bumpHeightFraction;
            _bumpDuration = bumpDuration;
            _destroyDelay = destroyDelay;
            _comboThreeWaveDuration = comboThreeWaveDuration;

            _gameManager.AddListener(this);
        }

        /// <summary>
        /// Продвигает bump, wave и delayed destroy только в gameplay time.
        /// </summary>
        public void OnUpdate(float deltaTime)
        {
            if (_isDisposed || _gameManager.State != GameState.PLAYING)
            {
                return;
            }

            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            _cameraShake.Tick(safeDeltaTime);
            if (_targets.Count == 0)
                return;

            for (int index = _targets.Count - 1; index >= 0; index--)
            {
                ImpactTarget target = _targets[index];
                ImpactTarget.TickResult result = target.Tick(safeDeltaTime);
                if (result == ImpactTarget.TickResult.Pending)
                    continue;

                if (result == ImpactTarget.TickResult.ReadyToComplete &&
                    target.IsCurrentLiveSpawn() &&
                    target.CompletionOutcome ==
                    SkateboardInteractionPolicy.Outcome.Destroy)
                {
                    target.RestorePosition();
                    _hamster.DestroyObstacleBySuperAttackEvent?.Invoke(target.Obstacle);
                }

                RemoveTargetAt(index, restorePosition: true);
            }
        }

        /// <summary>
        /// Отменяет pending impacts при завершении забега.
        /// </summary>
        public void OnFinish()
        {
            Cancel();
        }

        public void OnPause()
        {
            _cameraShake.SetPaused(isPaused: true);
        }

        public void OnResume()
        {
            _cameraShake.SetPaused(isPaused: false);
        }

        /// <summary>
        /// Отменяет незавершённую волну и возвращает временные offsets.
        /// </summary>
        public void Cancel()
        {
            ClearTargets(restorePosition: true);
            _cameraShake.Stop();
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _gameManager.RemoveListener(this);
            Cancel();
        }

        /// <summary>
        /// Исполняет landing miss и создаёт target snapshot для одной wave.
        /// </summary>
        public void StartImpact(in ImpactRequest request)
        {
            if (_isDisposed || _gameManager.State != GameState.PLAYING)
                return;

            ExecuteLandingMiss(request);

            int clampedComboDepth = Mathf.Clamp(
                request.ComboDepth,
                1,
                _maximumComboDepth);
            float halfScreenHeight = _camera.orthographicSize;
            float halfScreenWidth = halfScreenHeight * _camera.aspect;
            float screenLeft = _camera.transform.position.x - halfScreenWidth;
            float screenRight = _camera.transform.position.x + halfScreenWidth;
            float screenBottom = _camera.transform.position.y - halfScreenHeight;
            float screenTop = _camera.transform.position.y + halfScreenHeight;
            float hamsterX = _hamster.transform.position.x;
            float hamsterWidth = Mathf.Abs(_hamster.RightX - _hamster.LeftX);
            float radius = hamsterWidth * ResolveRadiusInHamsterWidths(
                clampedComboDepth,
                request.IsSuperCycle);
            float rightCaptureTravel = clampedComboDepth == _maximumComboDepth
                ? ResolveMaximumPendingLifecycle() *
                  Consts.RoadScrollSpeed *
                  ScrollLeftMechanics.SpeedMultiplier
                : 0f;
            float impactRight = screenRight + rightCaptureTravel;
            float maximumWaveDistance = Mathf.Max(
                Mathf.Abs(hamsterX - screenLeft),
                Mathf.Abs(impactRight - hamsterX));
            List<InstantiatedObstacle> spawnedObstacles = _obstacleSpawner.SpawnedObstacles;
            for (int index = 0; index < spawnedObstacles.Count; index++)
            {
                InstantiatedObstacle spawnEntry = spawnedObstacles[index];
                Obstacle obstacle = spawnEntry?.ObstacleScript;
                if (!TryResolveWaveOutcome(
                        obstacle,
                        request,
                        out SkateboardInteractionPolicy.Outcome outcome) ||
                    HasPendingTarget(obstacle))
                    continue;

                BoxCollider2D collider = obstacle.GetComponentInChildren<BoxCollider2D>();
                if (collider == null || !collider.enabled)
                    continue;

                Bounds bounds = collider.bounds;
                if (!IsVisible(
                        bounds,
                        screenLeft,
                        screenRight,
                        screenBottom,
                        screenTop,
                        rightCaptureTravel))
                {
                    continue;
                }

                float distance = GetHorizontalDistance(hamsterX, bounds);
                if (clampedComboDepth < _maximumComboDepth &&
                    distance > radius &&
                    !IsRoofHazardSupportedInsideRadius(
                        obstacle,
                        spawnedObstacles,
                        hamsterX,
                        radius))
                {
                    continue;
                }

                float attenuationDistance =
                    clampedComboDepth == _maximumComboDepth
                        ? maximumWaveDistance
                        : radius;
                float normalizedDistance = attenuationDistance > 0f
                    ? Mathf.Clamp01(distance / attenuationDistance)
                    : 0f;
                float waveDelay = ResolveWaveDelay(
                    clampedComboDepth,
                    normalizedDistance,
                    _comboThreeWaveDuration) + ResolveStartJitter();
                float strength = ResolveStrength(
                    clampedComboDepth,
                    normalizedDistance);
                float bumpHeight = bounds.size.y *
                                   _bumpHeightFraction *
                                   _bumpHeightFeedbackMultiplier *
                                   strength;

                _targets.Add(new ImpactTarget(
                    _obstacleSpawner,
                    spawnEntry,
                    request.ActionId,
                    request.StartedOnRoof,
                    outcome,
                    bumpHeight,
                    waveDelay,
                    _bumpDuration,
                    _destroyDelay));
            }

            _cameraShake.Play(clampedComboDepth);
        }

        private void ExecuteLandingMiss(in ImpactRequest request)
        {
            Obstacle missedRoof = request.MissedRoof;
            if (missedRoof == null ||
                !missedRoof.isActiveAndEnabled ||
                missedRoof.ObstacleType == null)
            {
                return;
            }

            SkateboardInteractionPolicy.Outcome outcome =
                SkateboardInteractionPolicy.Decide(
                    missedRoof.ObstacleType.ObstacleTypeEnum,
                    SkateboardInteractionPolicy.Phase.LandingMiss,
                    request.StartedOnRoof);
            if (outcome == SkateboardInteractionPolicy.Outcome.Destroy)
            {
                _hamster.DestroyObstacleBySuperAttackEvent?.Invoke(missedRoof);
            }
        }

        private static bool TryResolveWaveOutcome(
            Obstacle obstacle,
            in ImpactRequest request,
            out SkateboardInteractionPolicy.Outcome outcome)
        {
            outcome = SkateboardInteractionPolicy.Outcome.Ignore;
            if (obstacle == null ||
                !obstacle.isActiveAndEnabled ||
                obstacle.ObstacleType == null)
            {
                return false;
            }

            outcome = SkateboardInteractionPolicy.Decide(
                obstacle.ObstacleType.ObstacleTypeEnum,
                SkateboardInteractionPolicy.Phase.LandingWave,
                request.StartedOnRoof,
                isCurrentSupport: ReferenceEquals(obstacle, request.CurrentSupport));
            return outcome is SkateboardInteractionPolicy.Outcome.Destroy
                or SkateboardInteractionPolicy.Outcome.BumpOnly;
        }

        private bool HasPendingTarget(Obstacle obstacle)
        {
            for (int index = 0; index < _targets.Count; index++)
            {
                if (_targets[index].Matches(obstacle))
                    return true;
            }

            return false;
        }

        private void RemoveTargetAt(int index, bool restorePosition)
        {
            ImpactTarget target = _targets[index];
            _targets.RemoveAt(index);
            target.Dispose(restorePosition);
        }

        private void ClearTargets(bool restorePosition)
        {
            for (int index = _targets.Count - 1; index >= 0; index--)
                _targets[index].Dispose(restorePosition);

            _targets.Clear();
        }

        private static bool IsVisible(
            Bounds bounds,
            float screenLeft,
            float screenRight,
            float screenBottom,
            float screenTop,
            float rightCaptureTravel)
        {
            return bounds.max.x >= screenLeft &&
                   bounds.min.x <= screenRight +
                   rightCaptureTravel +
                   (rightCaptureTravel > 0f ? bounds.size.x : 0f) &&
                   bounds.max.y >= screenBottom &&
                   bounds.min.y <= screenTop;
        }

        private float ResolveMaximumPendingLifecycle()
        {
            return _comboThreeWaveDuration +
                   _maximumStartJitter +
                   _bumpDuration +
                   _destroyDelay;
        }

        private static float GetHorizontalDistance(float originX, Bounds bounds)
        {
            if (originX < bounds.min.x)
                return bounds.min.x - originX;
            if (originX > bounds.max.x)
                return originX - bounds.max.x;
            return 0f;
        }

        private static bool IsRoofHazardSupportedInsideRadius(
            Obstacle obstacle,
            List<InstantiatedObstacle> spawnedObstacles,
            float originX,
            float radius)
        {
            if (obstacle.ObstacleType.ObstacleTypeEnum !=
                ObstacleTypeEnum.smallNotAliveRoadAndRoof)
            {
                return false;
            }

            BoxCollider2D hazardCollider =
                obstacle.GetComponentInChildren<BoxCollider2D>();
            if (hazardCollider == null || !hazardCollider.enabled)
                return false;

            Bounds hazardBounds = hazardCollider.bounds;
            for (int index = 0; index < spawnedObstacles.Count; index++)
            {
                Obstacle support = spawnedObstacles[index]?.ObstacleScript;
                if (support == null ||
                    !support.isActiveAndEnabled ||
                    support.ObstacleType == null ||
                    !SkateboardInteractionPolicy.IsRoof(
                        support.ObstacleType.ObstacleTypeEnum) ||
                    support.ObstacleType.IsTop != obstacle.ObstacleType.IsTop)
                {
                    continue;
                }

                BoxCollider2D supportCollider =
                    support.GetComponentInChildren<BoxCollider2D>();
                if (supportCollider == null || !supportCollider.enabled)
                    continue;

                Bounds supportBounds = supportCollider.bounds;
                if (CollisionUtils.IsOverlap(
                        hazardBounds.min.x,
                        hazardBounds.max.x,
                        supportBounds.min.x,
                        supportBounds.max.x) &&
                    GetHorizontalDistance(originX, supportBounds) <= radius)
                {
                    return true;
                }
            }

            return false;
        }

        private static float ResolveStrength(
            int comboDepth,
            float normalizedDistance)
        {
            if (comboDepth == 1)
            {
                return Mathf.Lerp(
                    1f,
                    _comboOneMinimumFalloff,
                    normalizedDistance);
            }

            if (comboDepth == 2)
            {
                float comboTwoFalloff = Mathf.Lerp(
                    1f,
                    _comboTwoMinimumFalloff,
                    normalizedDistance);
                return _comboTwoStrength * comboTwoFalloff;
            }

            if (comboDepth < _maximumComboDepth)
                return 1f;

            float comboThreeFalloff = Mathf.Lerp(
                1f,
                _comboThreeMinimumFalloff,
                normalizedDistance);
            return _comboThreeStrength * comboThreeFalloff;
        }

        private static float ResolveRadiusInHamsterWidths(
            int comboDepth,
            bool isSuperCycle)
        {
            float normalRadius = comboDepth == 2
                ? _comboTwoRadiusInHamsterWidths
                : comboDepth;
            return isSuperCycle && comboDepth < _maximumComboDepth
                ? normalRadius * _superJumpRadiusMultiplier
                : normalRadius;
        }

        private static float ResolveWaveDelay(
            int comboDepth,
            float normalizedDistance,
            float comboThreeWaveDuration)
        {
            float waveDuration;
            switch (comboDepth)
            {
                case 1:
                    waveDuration = _comboOneWaveDuration;
                    break;
                case 2:
                    waveDuration = _comboTwoWaveDuration;
                    break;
                default:
                    waveDuration = comboThreeWaveDuration;
                    break;
            }

            if (waveDuration <= 0f)
                return 0f;

            // Три группы дают читаемые отдельные удары: рядом, середина, дальняя граница.
            int groupIndex = Mathf.Min(
                _waveGroupCount - 1,
                Mathf.FloorToInt(normalizedDistance * _waveGroupCount));
            return waveDuration * groupIndex /
                   (_waveGroupCount - 1);
        }

        private float ResolveStartJitter()
        {
            return (float)_startJitterRandom.NextDouble() * _maximumStartJitter;
        }

        private sealed class ImpactTarget
        {
            public enum TickResult
            {
                Pending,
                Invalid,
                ReadyToComplete
            }

            private readonly ObstacleSpawner _obstacleSpawner;
            private readonly InstantiatedObstacle _spawnEntry;
            private readonly GameObject _identity;
            private readonly int _instanceId;
            private readonly long _actionId;
            private readonly bool _startedOnRoof;
            private readonly float _baseY;
            private readonly float _bumpHeight;
            private readonly float _waveDelay;
            private readonly float _bumpDuration;
            private readonly float _destroyDelay;

            private float _elapsedTime;
            private bool _invalidated;
            private bool _disposed;

            public Obstacle Obstacle { get; }
            public long ActionId => _actionId;
            public bool StartedOnRoof => _startedOnRoof;
            public SkateboardInteractionPolicy.Outcome CompletionOutcome { get; }

            public ImpactTarget(
                ObstacleSpawner obstacleSpawner,
                InstantiatedObstacle spawnEntry,
                long actionId,
                bool startedOnRoof,
                SkateboardInteractionPolicy.Outcome completionOutcome,
                float bumpHeight,
                float waveDelay,
                float bumpDuration,
                float destroyDelay)
            {
                _obstacleSpawner = obstacleSpawner ??
                    throw new ArgumentNullException(nameof(obstacleSpawner));
                _spawnEntry = spawnEntry ??
                    throw new ArgumentNullException(nameof(spawnEntry));
                Obstacle = spawnEntry.ObstacleScript ??
                    throw new ArgumentException(
                        "Spawn entry must contain an obstacle.",
                        nameof(spawnEntry));
                CompletionOutcome = completionOutcome;
                _actionId = actionId;
                _startedOnRoof = startedOnRoof;
                _identity = Obstacle.gameObject;
                _instanceId = Obstacle.GetInstanceID();
                _baseY = Obstacle.transform.position.y;
                _bumpHeight = Mathf.Max(0f, bumpHeight);
                _waveDelay = Mathf.Max(0f, waveDelay);
                _bumpDuration = bumpDuration;
                _destroyDelay = destroyDelay;

                Obstacle.OnObstacleUnspawned.Subscribe(OnObstacleUnspawned);
            }

            public bool Matches(Obstacle obstacle)
            {
                return !_disposed &&
                       !_invalidated &&
                       obstacle != null &&
                       obstacle == Obstacle &&
                       obstacle.GetInstanceID() == _instanceId;
            }

            public TickResult Tick(float deltaTime)
            {
                if (!IsIdentityAlive())
                    return TickResult.Invalid;

                _elapsedTime += deltaTime;
                if (_elapsedTime < _waveDelay)
                    return TickResult.Pending;

                float impactTime = _elapsedTime - _waveDelay;
                if (impactTime < _bumpDuration)
                {
                    float progress = Mathf.Clamp01(impactTime / _bumpDuration);
                    float arc = Mathf.Sin(progress * Mathf.PI);
                    SetWorldY(_baseY + _bumpHeight * arc);
                    return TickResult.Pending;
                }

                SetWorldY(_baseY);
                return impactTime >= _bumpDuration + _destroyDelay
                    ? TickResult.ReadyToComplete
                    : TickResult.Pending;
            }

            public bool IsCurrentLiveSpawn()
            {
                if (!IsIdentityAlive())
                    return false;

                List<InstantiatedObstacle> liveObstacles = _obstacleSpawner.SpawnedObstacles;
                for (int index = 0; index < liveObstacles.Count; index++)
                {
                    if (ReferenceEquals(liveObstacles[index], _spawnEntry) &&
                        liveObstacles[index]?.ObstacleScript == Obstacle)
                    {
                        return true;
                    }
                }

                return false;
            }

            public void Dispose(bool restorePosition)
            {
                if (_disposed)
                    return;

                if (restorePosition && IsCurrentLiveSpawn())
                    SetWorldY(_baseY);

                Obstacle.OnObstacleUnspawned.Unsubscribe(OnObstacleUnspawned);
                _disposed = true;
            }

            public void RestorePosition()
            {
                if (IsCurrentLiveSpawn())
                    SetWorldY(_baseY);
            }

            private bool IsIdentityAlive()
            {
                return !_disposed &&
                       !_invalidated &&
                       Obstacle != null &&
                       _identity != null &&
                       Obstacle.gameObject == _identity &&
                       Obstacle.GetInstanceID() == _instanceId &&
                       Obstacle.isActiveAndEnabled;
            }

            private void OnObstacleUnspawned(GameObject unspawnedObject)
            {
                if (unspawnedObject == _identity)
                    _invalidated = true;
            }

            private void SetWorldY(float worldY)
            {
                Vector3 position = Obstacle.transform.position;
                position.y = worldY;
                Obstacle.transform.position = position;
            }
        }
    }
}
