using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts;
using Assets.Scripts.Common;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
using Assets.Scripts.System.Resources;
using UnityEngine;

namespace Vues.GameCore
{
    /// <summary>
    /// Уничтожает препятствия впереди хомяка на текущей линии.
    /// </summary>
    public sealed class ElectricStrikeAttack : ISuperAttackRuntime
    {
        public const string EffectAddress = "ElectricStrikePrefab";
        public const int DefaultChargePerObstacle = 35;

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
            ChargePerObstacle = chargePerObstacle;
        }

        /// <summary>
        /// Создаёт эффект и запускает последовательное уничтожение препятствий.
        /// </summary>
        public bool TryActivate()
        {
            var hamster = LevelController.Instance.LevelData.Hamster;
            HelpMethods.CreateUltaEffect(_effectPrefab, hamster);

            var obstaclesInRange = FindObstaclesOnSameLaneInRange(hamster);
            if (obstaclesInRange.Any())
            {
                hamster.StartCoroutine(DestroyObstaclesWithDelay(obstaclesInRange, 0.1f));
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

        private static IEnumerator DestroyObstaclesWithDelay(List<Obstacle> obstacles, float delay)
        {
            foreach (var obstacle in obstacles)
            {
                var hamster = LevelController.Instance.LevelData.Hamster;
                hamster.DestroyObstacleEvent?.Invoke(obstacle);
                yield return new WaitForSeconds(delay);
            }
        }

        private static List<Obstacle> FindObstaclesOnSameLaneInRange(Hamster hamster)
        {
            var spawnedObstacles = ObstacleSpawner.Instance.SpawnedObstacles
                .Select(x => x.ObstacleScript)
                .ToList();
            var obstaclesInRange = new List<Obstacle>();

            foreach (var obstacle in spawnedObstacles)
            {
                if (obstacle.transform.position.x < hamster.transform.position.x)
                {
                    continue;
                }

                if (!HelpMethods.IsOnSameLine(hamster.IsOnBottomLine.Value, obstacle))
                {
                    continue;
                }

                float distX = Mathf.Abs(hamster.transform.position.x - obstacle.transform.position.x);
                if (distX <= Consts.StrikeRangeMax)
                {
                    obstaclesInRange.Add(obstacle);
                }
            }

            return obstaclesInRange;
        }
    }
}
