using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
using Assets.Scripts.System.Resources;
using UnityEngine;

namespace Vues.GameCore
{
    /// <summary>
    /// Уничтожает случайные 30–70% активных препятствий на текущей линии хомяка.
    /// </summary>
    public sealed class ElectricStrikeAttack : ISuperAttackRuntime
    {
        public const string EffectAddress = "ElectricStrikePrefab";
        public const int DefaultChargePerObstacle = 35;

        private const int _minDestroyedPercentage = 30;
        private const int _maxDestroyedPercentage = 70;

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
        /// Создаёт эффект и уничтожает случайные 30–70% препятствий текущей линии.
        /// </summary>
        public bool TryActivate()
        {
            var hamster = LevelController.Instance.LevelData.Hamster;
            HelpMethods.CreateUltaEffect(_effectPrefab, hamster);

            var selectedObstacles = FindRandomObstaclesOnSameLane(hamster);
            if (selectedObstacles.Any())
            {
                hamster.StartCoroutine(DestroyObstaclesWithDelay(selectedObstacles, 0.1f));
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
                hamster.DestroyObstacleBySuperAttackEvent?.Invoke(obstacle);
                yield return new WaitForSeconds(delay);
            }
        }

        private static List<Obstacle> FindRandomObstaclesOnSameLane(Hamster hamster)
        {
            // Собираем активные обычные препятствия только с текущей линии.
            var sameLaneObstacles = ObstacleSpawner.Instance.SpawnedObstacles
                .Select(x => x.ObstacleScript)
                .Where(obstacle => obstacle != null
                    && obstacle.isActiveAndEnabled
                    && IsRegularObstacle(obstacle)
                    && HelpMethods.IsOnSameLine(hamster.IsOnBottomLine.Value, obstacle))
                .ToList();

            if (sameLaneObstacles.Count == 0)
                return sameLaneObstacles;

            // Выбираем случайное количество целей в диапазоне 30–70%.
            int minTargetCount = Mathf.Max(
                1,
                Mathf.CeilToInt(
                    sameLaneObstacles.Count * _minDestroyedPercentage / 100f));
            int maxTargetCount = Mathf.Max(
                minTargetCount,
                Mathf.FloorToInt(
                    sameLaneObstacles.Count * _maxDestroyedPercentage / 100f));
            int targetCount = UnityEngine.Random.Range(
                minTargetCount,
                maxTargetCount + 1);

            // Перемешиваем цели и возвращаем рассчитанную долю.
            for (int i = sameLaneObstacles.Count - 1; i > 0; i--)
            {
                int randomIndex = UnityEngine.Random.Range(0, i + 1);
                (sameLaneObstacles[i], sameLaneObstacles[randomIndex]) =
                    (sameLaneObstacles[randomIndex], sameLaneObstacles[i]);
            }

            return sameLaneObstacles.Take(targetCount).ToList();
        }

        private static bool IsRegularObstacle(Obstacle obstacle)
        {
            switch (obstacle.ObstacleType.ObstacleTypeEnum)
            {
                case ObstacleTypeEnum.smallAlive:
                case ObstacleTypeEnum.bigAlive:
                case ObstacleTypeEnum.smallNotAliveRoad:
                case ObstacleTypeEnum.smallNotAliveRoadAndRoof:
                case ObstacleTypeEnum.bigNotAlive:
                case ObstacleTypeEnum.mediumNotAlive:
                    return true;
                default:
                    return false;
            }
        }
    }
}
