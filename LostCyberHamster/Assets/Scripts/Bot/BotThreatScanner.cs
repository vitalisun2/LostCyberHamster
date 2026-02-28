using System.Collections.Generic;
using Assets.Scripts.Common.Models;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using UnityEngine;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Сканирует зону перед хомяком и классифицирует все препятствия/коллектиблы.
    /// Результат — списки ThreatInfo, разделённые по линиям.
    /// </summary>
    public class BotThreatScanner
    {
        private readonly List<ThreatInfo> _currentLane = new(16);
        private readonly List<ThreatInfo> _otherLane = new(16);
        private readonly List<ThreatInfo> _allThreats = new(32);

        public IReadOnlyList<ThreatInfo> CurrentLaneThreats => _currentLane;
        public IReadOnlyList<ThreatInfo> OtherLaneThreats => _otherLane;
        public IReadOnlyList<ThreatInfo> AllThreats => _allThreats;

        /// <summary>
        /// Сканирует все активные препятствия в зоне видимости.
        /// </summary>
        /// <param name="hamster">Хомяк.</param>
        /// <param name="scanDistance">Дальность сканирования в юнитах.</param>
        public void Scan(Hamster hamster, float scanDistance)
        {
            _currentLane.Clear();
            _otherLane.Clear();
            _allThreats.Clear();

            if (ObstacleSpawner.Instance == null) return;

            var spawned = ObstacleSpawner.Instance.SpawnedObstacles;
            float hamsterRightX = hamster.RightX;
            float maxX = hamsterRightX + scanDistance;
            bool isOnBottom = hamster.IsOnBottomLine.Value;

            for (int i = 0; i < spawned.Count; i++)
            {
                var inst = spawned[i];
                var obstacle = inst.ObstacleScript;
                float obstacleLeftX = GetObstacleLeftX(obstacle);

                // За зоной сканирования
                if (obstacleLeftX > maxX) continue;

                // Позади хомяка
                float obstacleRightX = obstacleLeftX + obstacle.ColliderWidth;
                if (obstacleRightX < hamster.LeftX) continue;

                float distance = obstacleLeftX - hamsterRightX;
                if (distance < 0f) distance = 0f;

                float timeToReach = distance / Consts.GameSpeedBase;

                bool isOnSameLine = IsOnSameLine(isOnBottom, obstacle);

                var info = new ThreatInfo
                {
                    Obstacle = obstacle,
                    Type = obstacle.ObstacleType.ObstacleTypeEnum,
                    DistanceX = distance,
                    TimeToReach = timeToReach,
                    IsOnCurrentLane = isOnSameLine,
                    IsOnOtherLane = !isOnSameLine,
                    IsCollectable = IsCollectable(obstacle.ObstacleType.ObstacleTypeEnum),
                    IsSmallAlive = obstacle.ObstacleType.ObstacleTypeEnum == ObstacleTypeEnum.smallAlive,
                    IsRoofable = IsRoofable(obstacle.ObstacleType.ObstacleTypeEnum),
                    IsDangerous = IsDangerous(obstacle.ObstacleType.ObstacleTypeEnum)
                };

                _allThreats.Add(info);

                if (isOnSameLine)
                    _currentLane.Add(info);
                else
                    _otherLane.Add(info);
            }
        }

        private static float GetObstacleLeftX(Obstacle obstacle)
        {
            var box = obstacle.GetComponentInChildren<BoxCollider2D>();
            if (box == null) return obstacle.transform.position.x;
            return box.bounds.min.x;
        }

        private static bool IsOnSameLine(bool hamsterIsOnBottom, Obstacle obstacle)
        {
            bool isTopObstacle = obstacle.ObstacleType.IsTop;
            bool isHamsterOnTop = !hamsterIsOnBottom;
            return (isTopObstacle && isHamsterOnTop) || (!isTopObstacle && !isHamsterOnTop);
        }

        private static bool IsCollectable(ObstacleTypeEnum type)
        {
            return type == ObstacleTypeEnum.collectableEnergetic ||
                   type == ObstacleTypeEnum.collectablePizza ||
                   type == ObstacleTypeEnum.collectableCrystal ||
                   type == ObstacleTypeEnum.collectableLife ||
                   type == ObstacleTypeEnum.collectableCoin;
        }

        private static bool IsRoofable(ObstacleTypeEnum type)
        {
            return type == ObstacleTypeEnum.bigNotAlive ||
                   type == ObstacleTypeEnum.mediumNotAlive;
        }

        private static bool IsDangerous(ObstacleTypeEnum type)
        {
            return type == ObstacleTypeEnum.smallAlive ||
                   type == ObstacleTypeEnum.bigAlive ||
                   type == ObstacleTypeEnum.smallNotAliveRoad ||
                   type == ObstacleTypeEnum.smallNotAliveRoadAndRoof;
        }
    }
}
