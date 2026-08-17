using System;
using System.Collections.Generic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
using UnityEngine;

namespace Vues.GameCore
{
    /// <summary>
    /// Владеет индивидуальным bounce и delayed outcome одной obstacle spawn identity.
    /// </summary>
    internal sealed class SkateboardLandingImpactTarget
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
        private readonly float _baseY;
        private readonly float _bumpHeight;
        private readonly float _waveDelay;
        private readonly float _bumpDuration;
        private readonly float _destroyDelay;

        private float _elapsedTime;
        private bool _invalidated;
        private bool _disposed;

        public Obstacle Obstacle { get; }
        public SkateboardInteractionPolicy.Outcome CompletionOutcome { get; }

        public SkateboardLandingImpactTarget(
            ObstacleSpawner obstacleSpawner,
            InstantiatedObstacle spawnEntry,
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
