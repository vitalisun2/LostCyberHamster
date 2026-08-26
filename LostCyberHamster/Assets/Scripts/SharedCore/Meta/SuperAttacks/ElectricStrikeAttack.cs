using System;
using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Common;
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

        private const float _delayBetweenDestroyedObstacles = 0.1f;
        private const int _twoDropsMinimumDestroyedCount = 4;

        private readonly AddressableLease<GameObject> _effectPrefabLease;
        private readonly GameObject _effectPrefab;

        /// <summary>
        /// Возвращает заряд за одно уничтоженное препятствие.
        /// </summary>
        public int ChargePerObstacle { get; }

        /// <summary>
        /// Возвращает признак длительной активности, которой у удара нет.
        /// </summary>
        public bool IsActive => false;

        /// <summary>
        /// Создаёт электрический удар и принимает владение lease prefab эффекта.
        /// </summary>
        public ElectricStrikeAttack(
            AddressableLease<GameObject> effectPrefabLease,
            int chargePerObstacle = DefaultChargePerObstacle)
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

            ChargePerObstacle = chargePerObstacle;
        }

        /// <summary>
        /// Создаёт эффект и запускает волну разрушения от ближних целей к дальним.
        /// </summary>
        public bool TryActivate()
        {
            var hamster = LevelController.Instance.LevelData.Hamster;
            GameObject effectObject = HelpMethods.CreateUltaEffect(_effectPrefab, hamster);
            ElectricStrikeUlta effect = effectObject.GetComponent<ElectricStrikeUlta>();

            ObstacleSpawner obstacleSpawner = ObstacleSpawner.Instance;
            List<InstantiatedObstacle> obstacles = FindObstaclesWithinEffect(
                hamster,
                obstacleSpawner,
                effect.WorldRightEdge);
            if (obstacles.Count > 0)
            {
                hamster.StartCoroutine(DestroyObstaclesWithDelay(
                    hamster,
                    obstacleSpawner,
                    obstacles,
                    _delayBetweenDestroyedObstacles));
            }

            return true;
        }

        /// <summary>
        /// Не выполняет обновление для мгновенного суперудара.
        /// </summary>
        public void Update()
        {
        }

        /// <summary>
        /// Освобождает lease prefab эффекта.
        /// </summary>
        public void Dispose()
        {
            _effectPrefabLease.Dispose();
        }

        private static IEnumerator DestroyObstaclesWithDelay(
            Hamster hamster,
            ObstacleSpawner obstacleSpawner,
            List<InstantiatedObstacle> obstacles,
            float delay)
        {
            var destructionDelay = new WaitForSeconds(delay);
            var destroyedObstacles = new List<Obstacle>(obstacles.Count);
            for (int index = 0; index < obstacles.Count; index++)
            {
                InstantiatedObstacle target = obstacles[index];
                if (!IsCurrentLiveTarget(hamster, obstacleSpawner, target))
                    continue;

                Obstacle obstacle = target.ObstacleScript;
                hamster.DestroyObstacleBySuperAttackEvent?.Invoke(obstacle);
                destroyedObstacles.Add(obstacle);
                yield return destructionDelay;
            }

            ApplyRandomDrops(hamster, destroyedObstacles);
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

        private static void ApplyRandomDrops(
            Hamster hamster,
            List<Obstacle> destroyedObstacles)
        {
            int dropCount = destroyedObstacles.Count >= _twoDropsMinimumDestroyedCount
                ? 2
                : destroyedObstacles.Count > 0
                    ? 1
                    : 0;

            for (int index = 0; index < dropCount; index++)
            {
                int randomIndex = UnityEngine.Random.Range(index, destroyedObstacles.Count);
                (destroyedObstacles[index], destroyedObstacles[randomIndex]) =
                    (destroyedObstacles[randomIndex], destroyedObstacles[index]);
                hamster.ObstacleBonusDropEvent?.Invoke(destroyedObstacles[index]);
            }
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
