using System;
using System.Collections.Generic;
using Assets.Scripts.Common;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using Assets.Scripts.System.Resources;
using UnityEngine;

namespace Vues.GameCore
{
    /// <summary>
    /// Уничтожает физические препятствия перед хомяком в пределах электрического эффекта.
    /// </summary>
    public sealed class ElectricStrikeAttack : ISuperAttackRuntime
    {
        public const string EffectAddress = "ElectricStrikePrefab";
        public const int DefaultChargePerObstacle = 35;

        private readonly SuperAttackData _data;
        private readonly Hamster _hamster;
        private readonly GameManager _gameManager;
        private readonly ObstacleSpawner _spawner;
        private readonly List<GameObject> _effects = new();
        private SuperAttackLevelData _level;
        private SuperAttackDropBudget _drops;
        private long _activationId;
        private bool _applying;
        private bool _disposed;

        private readonly AddressableLease<GameObject> _effectPrefabLease;
        private readonly GameObject _effectPrefab;

        /// <summary>
        /// Возвращает заряд за одно уничтоженное препятствие.
        /// </summary>
        public int ChargePerObstacle => _data.UltaCharge;
        public SuperAttackRuntimeSnapshot Snapshot => new(_data.Id, _level?.Level ?? 1, _activationId,
            false, 0, 0, destroyedCount: _drops?.DestroyedCount ?? 0, dropsCreated: _drops?.DropsCreated ?? 0);

        /// <summary>
        /// Возвращает признак длительной активности, которой у удара нет.
        /// </summary>
        public bool IsActive => false;

        /// <summary>
        /// Создаёт электрический удар и принимает владение lease prefab эффекта.
        /// </summary>
        public ElectricStrikeAttack(
            AddressableLease<GameObject> effectPrefabLease,
            SuperAttackData data, Hamster hamster, GameManager gameManager, ObstacleSpawner spawner)
        {
            _effectPrefabLease = effectPrefabLease ??
                throw new ArgumentNullException(nameof(effectPrefabLease));
            _effectPrefab = effectPrefabLease.Value ??
                throw new ArgumentException(
                    "Lease не содержит prefab эффекта.",
                    nameof(effectPrefabLease));

            ElectricStrikeUlta effect = _effectPrefab.GetComponent<ElectricStrikeUlta>();
            if (effect == null || !effect.IsConfigured)
            {
                throw new ArgumentException(
                    "Prefab эффекта не содержит настроенный ElectricStrikeUlta.",
                    nameof(effectPrefabLease));
            }

            _data = data ?? throw new ArgumentNullException(nameof(data));
            _hamster = hamster ?? throw new ArgumentNullException(nameof(hamster));
            _gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
            _spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
        }

        /// <summary>
        /// Создаёт эффект и запускает волну разрушения от ближних целей к дальним.
        /// </summary>
        public bool TryActivate()
        {
            if (_disposed || _applying || _gameManager.State != GameState.PLAYING) return false;
            _level = SuperAttackLevelResolver.GetEffective(_data);
            _drops = new SuperAttackDropBudget(_level);
            _activationId++;
            _effects.RemoveAll(item => item == null);
            var effectObject = HelpMethods.CreateUltaEffect(_effectPrefab, _hamster);
            _effects.Add(effectObject);
            var effect = effectObject.GetComponent<ElectricStrikeUlta>();
            effect.LockWorldY(effectObject.transform.position.y);
            effect.SetRangeMultiplier(_hamster.RightX, _level.RangeMultiplier);

            // Физическое действие мгновенно; последовательный световой эффект живёт отдельно.
            var targets = FindObstaclesWithinEffect(_hamster, _spawner, effect.WorldRightEdge);
            _applying = true;
            try
            {
                foreach (var target in targets)
                    if (IsCurrentLiveTarget(_hamster, _spawner, target))
                        _hamster.DestroyObstacleBySuperAttackEvent.Invoke(target.ObstacleScript);
            }
            finally { _applying = false; }
            return true;
        }

        public void OnObstacleDestroyed(Obstacle obstacle)
        {
            if (_applying && _drops.TryCreateDrop(obstacle))
                _hamster.ObstacleBonusDropEvent.Invoke(obstacle);
        }

        public void Update() { }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var effect in _effects)
                if (effect != null) UnityEngine.Object.Destroy(effect);
            _effects.Clear();
            _effectPrefabLease.Dispose();
        }

        private static List<InstantiatedObstacle> FindObstaclesWithinEffect(
            Hamster hamster,
            ObstacleSpawner obstacleSpawner,
            float effectRightEdge)
        {
            var obstacles = new List<InstantiatedObstacle>();
            foreach (InstantiatedObstacle spawnedObstacle in obstacleSpawner.SpawnedObstacles)
            {
                Obstacle obstacle = spawnedObstacle?.ObstacleScript;
                if (!IsTargetObstacle(hamster, obstacle, effectRightEdge))
                    continue;

                obstacles.Add(spawnedObstacle);
            }

            obstacles.Sort(CompareByLeftEdge);
            return obstacles;
        }

        private static bool IsTargetObstacle(
            Hamster hamster,
            Obstacle obstacle,
            float effectRightEdge)
        {
            if (obstacle == null ||
                !obstacle.isActiveAndEnabled ||
                obstacle.ObstacleType == null ||
                !ObstacleTypePolicy.IsPhysical(obstacle.ObstacleType.ObstacleTypeEnum) ||
                !HelpMethods.IsOnSameLine(hamster.IsOnBottomLine.Value, obstacle) ||
                IsReservedMovementTarget(hamster, obstacle))
            {
                return false;
            }

            BoxCollider2D collider = obstacle.GetComponentInChildren<BoxCollider2D>();
            if (collider == null || !collider.enabled)
                return false;

            Bounds bounds = collider.bounds;
            return bounds.max.x > hamster.RightX &&
                   bounds.min.x <= effectRightEdge;
        }

        private static bool IsReservedMovementTarget(Hamster hamster, Obstacle obstacle)
        {
            if (ReferenceEquals(obstacle, hamster.PendingJumpedOnObstacle.Value))
                return true;

            if (!ReferenceEquals(obstacle, hamster.LastObstacle.Value))
                return false;

            return hamster.HamsterState.Value is HamsterStateEnum.JumpOnRoof
                or HamsterStateEnum.JumpOnRoofDamage
                or HamsterStateEnum.RoofRun
                or HamsterStateEnum.RoofJump
                or HamsterStateEnum.RoofJumpDamage
                or HamsterStateEnum.SuperJumpOnRoof
                or HamsterStateEnum.SuperJumpOnRoofDamage
                or HamsterStateEnum.SuperRoofJump
                or HamsterStateEnum.SuperRoofJumpDamage;
        }

        private static int CompareByLeftEdge(
            InstantiatedObstacle left,
            InstantiatedObstacle right)
        {
            float leftEdge = left.ObstacleScript
                .GetComponentInChildren<BoxCollider2D>().bounds.min.x;
            float rightEdge = right.ObstacleScript
                .GetComponentInChildren<BoxCollider2D>().bounds.min.x;
            return leftEdge.CompareTo(rightEdge);
        }

        private static bool IsCurrentLiveTarget(
            Hamster hamster,
            ObstacleSpawner obstacleSpawner,
            InstantiatedObstacle target)
        {
            Obstacle obstacle = target?.ObstacleScript;
            return obstacle != null &&
                   obstacle.isActiveAndEnabled &&
                   !IsReservedMovementTarget(hamster, obstacle) &&
                   obstacleSpawner.SpawnedObstacles.Contains(target);
        }
    }
}
