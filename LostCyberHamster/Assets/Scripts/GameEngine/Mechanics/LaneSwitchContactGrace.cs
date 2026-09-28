using System.Collections.Generic;
using Assets.Scripts.Common;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;

namespace Assets.Scripts.GameEngine.Mechanics
{
    /// <summary>
    /// Хранит уходящие препятствия целевой линии, допущенные при старте смены линии.
    /// </summary>
    public sealed class LaneSwitchContactGrace
    {
        private readonly HashSet<Obstacle> _eligibleObstacles = new();
        private bool _isActive;
        private bool _targetBottomLine;
        private float _hamsterLeftX;
        private float _hamsterRightX;

        /// <summary>
        /// Фиксирует допустимые препятствия целевой линии в момент tap.
        /// </summary>
        public void Begin(bool targetBottomLine, float hamsterLeftX, float hamsterRightX)
        {
            Clear();
            _isActive = true;
            _targetBottomLine = targetBottomLine;
            _hamsterLeftX = hamsterLeftX;
            _hamsterRightX = hamsterRightX;

            // Фиксируем препятствия целевой линии, подходящие под общий порог.
            if (ObstacleSpawner.Instance == null)
                return;

            foreach (var spawned in ObstacleSpawner.Instance.SpawnedObstacles)
            {
                Obstacle obstacle = spawned?.ObstacleScript;
                if (obstacle == null || !obstacle.isActiveAndEnabled
                    || !HelpMethods.IsOnSameLine(targetBottomLine, obstacle))
                {
                    continue;
                }

                CollisionUtils.GetObstacleXInterval(
                    obstacle, obstacle.ColliderWidth, 0f,
                    out float obstacleLeftX, out float obstacleRightX);

                if (LaneSwitchCollisionRule.IsEligibleAtShiftStart(
                        hamsterLeftX, hamsterRightX, obstacleLeftX, obstacleRightX))
                {
                    _eligibleObstacles.Add(obstacle);
                }
            }
        }

        /// <summary>
        /// Проверяет льготу для контакта с препятствием во время текущего перехода.
        /// </summary>
        public bool CanIgnore(Obstacle obstacle, bool isOnBottomLine, bool isShifting)
        {
            // Проверяем принадлежность контакта текущему переходу.
            if (!_isActive || !isShifting || isOnBottomLine != _targetBottomLine
                || obstacle == null || !_eligibleObstacles.Contains(obstacle))
            {
                return false;
            }

            // Сохраняем льготу, пока препятствие остаётся уходящим.
            CollisionUtils.GetObstacleXInterval(
                obstacle, obstacle.ColliderWidth, 0f,
                out float obstacleLeftX, out float obstacleRightX);
            return LaneSwitchCollisionRule.IsEligibleAtShiftStart(
                _hamsterLeftX, _hamsterRightX, obstacleLeftX, obstacleRightX);
        }

        /// <summary>
        /// Завершает льготу вместе со сменой линии.
        /// </summary>
        public void Clear()
        {
            _isActive = false;
            _eligibleObstacles.Clear();
        }
    }
}
