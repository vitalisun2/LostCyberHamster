using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts;
using Assets.Scripts.Common;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
using UnityEngine;

namespace Vues.GameCore
{
    public sealed class ElectricStrikeAttack : ISuperAttackRuntime
    {
        public const string EffectAddress = "ElectricStrikePrefab";
        public const int DefaultChargePerObstacle = 35;

        private readonly GameObject _effectPrefab;

        public int ChargePerObstacle { get; }
        public bool IsActive => false;

        public ElectricStrikeAttack(
            GameObject effectPrefab,
            int chargePerObstacle = DefaultChargePerObstacle)
        {
            _effectPrefab = effectPrefab;
            ChargePerObstacle = chargePerObstacle;
        }

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

        public void Update()
        {
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
