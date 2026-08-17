using System;
using System.Collections.Generic;
using Assets.Scripts;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
using UnityEngine;

namespace Vues.GameCore
{
    /// <summary>
    /// Подбрасывает видимые препятствия при skateboard landing и уничтожает их с задержкой.
    /// </summary>
    internal sealed class SkateboardLandingImpactRuntime :
        Listeners.IGameUpdateListener,
        Listeners.IGamePauseListener,
        Listeners.IGameResumeListener,
        IDisposable
    {
        public const float DefaultBumpHeightFraction = 0.1f;
        public const float DefaultBumpDuration = 5f / 60f;
        public const float DefaultDestroyDelay = 3f / 60f;

        /// <summary>
        /// Базовая пауза в кадрах при 60 FPS от landing impact до старта
        /// ближайшей obstacle wave. Дистанционная задержка добавляется поверх неё.
        /// </summary>
        public const int DefaultWaveDelayAfterLandingImpactFrames = 0;

        private const float DefaultWaveDelayAfterLandingImpact = 0f;

        public const float DefaultWaveDuration = 0.39f;

        private const float _superImpactTimelineScale = 2f;
        private const float _cameraShakeTimeScale = 2f;
        private const float _cameraShakeOscillationCountScale = 0.5f;

        private const float _bumpHeightFeedbackMultiplier = 2.6f;
        private const float _minimumFalloff = 0.45f;
        private const float _normalDestroyDistanceScale = 0.7f;
        private const float _superJumpStrength = 2f;
        private const float _normalCameraShakeStrength = 2f;
        private const float _superCameraShakeStrength = 4f;

        private readonly Hamster _hamster;
        private readonly ObstacleSpawner _obstacleSpawner;
        private readonly GameManager _gameManager;
        private readonly Camera _camera;
        private readonly ICameraShake _cameraShake;
        private readonly float _bumpHeightFraction;
        private readonly float _bumpDuration;
        private readonly float _destroyDelay;
        private readonly float _waveDelayAfterLandingImpact;
        private readonly float _waveDuration;
        private readonly List<SkateboardLandingImpactTarget> _targets = new(32);

        private bool _isDisposed;

        /// <summary>
        /// Подключает runtime к gameplay update loop с явными dependencies.
        /// </summary>
        public SkateboardLandingImpactRuntime(
            Hamster hamster,
            ObstacleSpawner obstacleSpawner,
            GameManager gameManager,
            Camera camera,
            ICameraShake cameraShake,
            float bumpHeightFraction = DefaultBumpHeightFraction,
            float bumpDuration = DefaultBumpDuration,
            float destroyDelay = DefaultDestroyDelay,
            float waveDuration = DefaultWaveDuration,
            float waveDelayAfterLandingImpact = DefaultWaveDelayAfterLandingImpact)
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

            if (waveDuration < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(waveDuration),
                    waveDuration,
                    "Wave duration must not be negative.");
            }

            if (waveDelayAfterLandingImpact < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(waveDelayAfterLandingImpact),
                    waveDelayAfterLandingImpact,
                    "Wave delay after landing impact must not be negative.");
            }

            _bumpHeightFraction = bumpHeightFraction;
            _bumpDuration = bumpDuration;
            _destroyDelay = destroyDelay;
            _waveDuration = waveDuration;
            _waveDelayAfterLandingImpact = waveDelayAfterLandingImpact;

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
                SkateboardLandingImpactTarget target = _targets[index];
                SkateboardLandingImpactTarget.TickResult result =
                    target.Tick(safeDeltaTime);
                if (result == SkateboardLandingImpactTarget.TickResult.Pending)
                    continue;

                if (result == SkateboardLandingImpactTarget.TickResult.ReadyToComplete &&
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
        /// Создаёт snapshot видимых obstacle targets для одной landing wave.
        /// </summary>
        public void StartImpact(in SkateboardLandingImpactRequest request)
        {
            if (_isDisposed || _gameManager.State != GameState.PLAYING)
                return;

            // Фиксируем единые времена и пространственные границы текущего impact.
            SkateboardLandingImpactTimeline timeline =
                ResolveImpactTimeline(request.IsSuperCycle);
            float halfScreenHeight = _camera.orthographicSize;
            float halfScreenWidth = halfScreenHeight * _camera.aspect;
            float screenLeft = _camera.transform.position.x - halfScreenWidth;
            float screenRight = _camera.transform.position.x + halfScreenWidth;
            float screenBottom = _camera.transform.position.y - halfScreenHeight;
            float screenTop = _camera.transform.position.y + halfScreenHeight;
            float hamsterLeft = _hamster.LeftX;
            float hamsterRight = _hamster.RightX;
            float viewportWidth = screenRight - screenLeft;
            float normalDestroyRight = hamsterRight +
                                       Mathf.Max(0f, screenRight - hamsterRight) *
                                       _normalDestroyDistanceScale;
            float waveRight = request.IsSuperCycle
                ? screenRight + viewportWidth / 3f
                : screenRight;
            float maximumWaveDistance = Mathf.Max(
                Mathf.Max(0f, hamsterLeft - screenLeft),
                Mathf.Max(0f, waveRight - hamsterRight));

            // Собираем разовый target snapshot с distance falloff и scaled timeline.
            List<InstantiatedObstacle> spawnedObstacles = _obstacleSpawner.SpawnedObstacles;
            for (int index = 0; index < spawnedObstacles.Count; index++)
            {
                InstantiatedObstacle spawnEntry = spawnedObstacles[index];
                Obstacle obstacle = spawnEntry?.ObstacleScript;
                if (obstacle == null ||
                    !obstacle.isActiveAndEnabled ||
                    obstacle.ObstacleType == null)
                {
                    continue;
                }

                BoxCollider2D collider = obstacle.GetComponentInChildren<BoxCollider2D>();
                if (collider == null || !collider.enabled)
                    continue;

                Bounds bounds = collider.bounds;
                if (!IntersectsWaveBounds(
                        bounds,
                        screenLeft,
                        waveRight,
                        screenBottom,
                        screenTop))
                {
                    continue;
                }

                // Новая landing wave заменяет прежний timeline этой же цели.
                RemovePendingTarget(obstacle);
                bounds = collider.bounds;
                if (!IntersectsWaveBounds(
                        bounds,
                        screenLeft,
                        waveRight,
                        screenBottom,
                        screenTop))
                {
                    continue;
                }

                bool isDestructionArea = request.IsSuperCycle ||
                                         IsInsideNormalDestroyArea(
                                             bounds,
                                             hamsterRight,
                                             normalDestroyRight);
                if (!TryResolveWaveOutcome(
                        obstacle,
                        request,
                        isDestructionArea,
                        out SkateboardInteractionPolicy.Outcome outcome))
                {
                    continue;
                }

                float distance = GetHorizontalDistance(
                    hamsterLeft,
                    hamsterRight,
                    bounds);
                float normalizedDistance = maximumWaveDistance > 0f
                    ? Mathf.Clamp01(distance / maximumWaveDistance)
                    : 0f;
                float waveDelay = _waveDelayAfterLandingImpact +
                                  ResolveWaveDelay(
                                      normalizedDistance,
                                      timeline.WaveDuration);
                float strength = ResolveStrength(
                    request.IsSuperCycle,
                    normalizedDistance);
                float bumpHeight = bounds.size.y *
                                   _bumpHeightFraction *
                                   _bumpHeightFeedbackMultiplier *
                                   strength;

                _targets.Add(new SkateboardLandingImpactTarget(
                    _obstacleSpawner,
                    spawnEntry,
                    outcome,
                    bumpHeight,
                    waveDelay,
                    timeline.BumpDuration,
                    timeline.DestroyDelay));
            }

            // Wave snapshot и camera feedback имеют одну точку старта.
            _cameraShake.Play(
                request.IsSuperCycle
                    ? _superCameraShakeStrength
                    : _normalCameraShakeStrength,
                timeline.CameraShakeDurationMultiplier,
                timeline.CameraShakeFrequencyMultiplier);
        }

        private SkateboardLandingImpactTimeline ResolveImpactTimeline(bool isSuperCycle)
        {
            float scale = isSuperCycle ? _superImpactTimelineScale : 1f;
            // Частота компенсирует time stretch и оставляет половину прежних колебаний.
            return new SkateboardLandingImpactTimeline(
                _waveDuration * scale,
                _bumpDuration * scale,
                _destroyDelay * scale,
                _cameraShakeTimeScale,
                _cameraShakeOscillationCountScale / _cameraShakeTimeScale);
        }

        private static bool TryResolveWaveOutcome(
            Obstacle obstacle,
            in SkateboardLandingImpactRequest request,
            bool isDestructionArea,
            out SkateboardInteractionPolicy.Outcome outcome)
        {
            outcome = SkateboardInteractionPolicy.Outcome.Ignore;
            if (obstacle == null ||
                !obstacle.isActiveAndEnabled ||
                obstacle.ObstacleType == null)
            {
                return false;
            }

            outcome = SkateboardInteractionPolicy.DecideLandingWave(
                obstacle.ObstacleType.ObstacleTypeEnum,
                request.StartedOnRoof,
                ReferenceEquals(obstacle, request.CurrentSupport),
                isDestructionArea);
            return outcome is SkateboardInteractionPolicy.Outcome.Destroy
                or SkateboardInteractionPolicy.Outcome.BumpOnly;
        }

        private void RemovePendingTarget(Obstacle obstacle)
        {
            for (int index = _targets.Count - 1; index >= 0; index--)
            {
                if (_targets[index].Matches(obstacle))
                    RemoveTargetAt(index, restorePosition: true);
            }
        }

        private void RemoveTargetAt(int index, bool restorePosition)
        {
            SkateboardLandingImpactTarget target = _targets[index];
            _targets.RemoveAt(index);
            target.Dispose(restorePosition);
        }

        private void ClearTargets(bool restorePosition)
        {
            for (int index = _targets.Count - 1; index >= 0; index--)
                _targets[index].Dispose(restorePosition);

            _targets.Clear();
        }

        private static bool IntersectsWaveBounds(
            Bounds bounds,
            float screenLeft,
            float screenRight,
            float screenBottom,
            float screenTop)
        {
            return bounds.max.x >= screenLeft &&
                   bounds.min.x <= screenRight &&
                   bounds.max.y >= screenBottom &&
                   bounds.min.y <= screenTop;
        }

        private static bool IsInsideNormalDestroyArea(
            Bounds bounds,
            float hamsterRight,
            float destroyRight)
        {
            return bounds.max.x >= hamsterRight && bounds.min.x <= destroyRight;
        }

        private static float GetHorizontalDistance(
            float hamsterLeft,
            float hamsterRight,
            Bounds bounds)
        {
            if (hamsterRight < bounds.min.x)
                return bounds.min.x - hamsterRight;
            if (hamsterLeft > bounds.max.x)
                return hamsterLeft - bounds.max.x;
            return 0f;
        }

        private static float ResolveStrength(
            bool isSuperCycle,
            float normalizedDistance)
        {
            float falloff = Mathf.Lerp(
                1f,
                _minimumFalloff,
                normalizedDistance);
            return (isSuperCycle ? _superJumpStrength : 1f) * falloff;
        }

        private static float ResolveWaveDelay(
            float normalizedDistance,
            float waveDuration)
        {
            if (waveDuration <= 0f)
                return 0f;

            return waveDuration * Mathf.Clamp01(normalizedDistance);
        }

    }
}
