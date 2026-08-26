using System;
using System.Collections.Generic;
using Assets.Scripts;
using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using UnityEngine;

namespace Assets.Scripts.Tutorial
{
    /// <summary>
    /// Изолирует tutorial FSM от gameplay singletons и команд Hamster.
    /// </summary>
    public sealed class TutorialGameplayWorldAdapter : ITutorialGameplayWorldAdapter
    {
        private readonly GameManager _gameManager;
        private readonly Hamster _hamster;

        public TutorialGameplayWorldAdapter(GameManager gameManager, Hamster hamster)
        {
            _gameManager = gameManager != null
                ? gameManager
                : throw new ArgumentNullException(nameof(gameManager));
            _hamster = hamster != null
                ? hamster
                : throw new ArgumentNullException(nameof(hamster));
        }

        public GameState State => _gameManager.State;

        public HamsterStateEnum HamsterState => _hamster.HamsterState.Value;

        public Obstacle FindNearestSameLineObstacle(IReadOnlyList<ObstacleTypeEnum> targetTypes)
        {
            ObstacleSpawner spawner = ObstacleSpawner.Instance;
            if (spawner == null)
            {
                return null;
            }

            Obstacle nearest = null;
            float nearestX = float.PositiveInfinity;
            float hamsterX = _hamster.transform.position.x;
            foreach (var spawned in spawner.SpawnedObstacles)
            {
                Obstacle obstacle = spawned?.ObstacleScript;
                if (obstacle == null
                    || !HelpMethods.IsOnSameLine(_hamster.IsOnBottomLine.Value, obstacle)
                    || !MatchesTargetType(obstacle, targetTypes))
                {
                    continue;
                }

                float obstacleX = obstacle.transform.position.x;
                if (obstacleX <= hamsterX || obstacleX >= nearestX)
                {
                    continue;
                }

                nearest = obstacle;
                nearestX = obstacleX;
            }

            return nearest;
        }

        public float GetDistanceToHamster(Obstacle obstacle)
        {
            CollisionUtils.GetObstacleXInterval(
                obstacle,
                obstacle.ColliderWidth,
                0f,
                out float obstacleLeftX,
                out _);
            return obstacleLeftX - _hamster.RightX;
        }

        public bool HasObstacleLeftPlay(Obstacle obstacle)
        {
            if (obstacle == null)
            {
                return true;
            }

            ObstacleSpawner spawner = ObstacleSpawner.Instance;
            if (spawner == null || !ContainsObstacle(spawner, obstacle))
            {
                return true;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return false;
            }

            CollisionUtils.GetObstacleXInterval(
                obstacle,
                obstacle.ColliderWidth,
                0f,
                out _,
                out float obstacleRightX);
            float screenLeftEdge = mainCamera.transform.position.x
                                   - mainCamera.orthographicSize * mainCamera.aspect;
            return obstacleRightX < screenLeftEdge;
        }

        public void PerformAction(TutorialAction action)
        {
            switch (action)
            {
                case TutorialAction.Tap:
                    _hamster.TapRequest?.Invoke();
                    break;
                case TutorialAction.Jump:
                    PerformJump();
                    break;
                case TutorialAction.SuperJump:
                    PerformSuperJump();
                    break;
            }
        }

        public void Pause()
        {
            _gameManager.Pause();
        }

        public void Resume()
        {
            _gameManager.Resume();
        }

        private static bool MatchesTargetType(
            Obstacle obstacle,
            IReadOnlyList<ObstacleTypeEnum> targetTypes)
        {
            if (targetTypes == null || targetTypes.Count == 0)
            {
                return true;
            }

            for (int index = 0; index < targetTypes.Count; index++)
            {
                if (targetTypes[index] == obstacle.ObstacleType.ObstacleTypeEnum)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsObstacle(ObstacleSpawner spawner, Obstacle obstacle)
        {
            foreach (var spawned in spawner.SpawnedObstacles)
            {
                if (spawned?.ObstacleScript == obstacle)
                {
                    return true;
                }
            }

            return false;
        }

        private void PerformJump()
        {
            if (_hamster.HamsterState.Value == HamsterStateEnum.RoofRun)
            {
                _hamster.RoofJumpRequest?.Invoke();
                return;
            }

            _hamster.JumpRequest?.Invoke();
        }

        private void PerformSuperJump()
        {
            HamsterStateEnum state = _hamster.HamsterState.Value;
            bool isJumpingFromRoof = state == HamsterStateEnum.RoofJump
                                     || state == HamsterStateEnum.RoofJumpDamage
                                     || state == HamsterStateEnum.JumpFromRoof
                                     || state == HamsterStateEnum.JumpFromRoofDamage
                                     || state == HamsterStateEnum.JumpOnObstacleFromRoof;
            if (isJumpingFromRoof)
            {
                _hamster.SuperRoofJumpRequest?.Invoke();
                return;
            }

            _hamster.SuperJumpRequest?.Invoke();
        }
    }
}
