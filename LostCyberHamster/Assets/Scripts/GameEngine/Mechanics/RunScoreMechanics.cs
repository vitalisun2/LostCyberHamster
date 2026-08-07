using Assets.Scripts.Common.Models;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Atomic.Elements;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Mechanics
{
    /// <summary>
    /// Считает очки текущего забега по подтверждённым игровым событиям.
    /// </summary>
    public sealed class RunScoreMechanics
    {
        private const int _coinScore = 1;
        private const int _bonusScore = 2;
        private const int _destroyedObstacleScore = 3;

        private readonly AtomicEvent<ObstacleTypeEnum> _collectableCollectedEvent;
        private readonly AtomicEvent<Obstacle> _destroyObstacleEvent;
        private readonly AtomicEvent<Obstacle> _destroyObstacleBySuperAttackEvent;
        private readonly AtomicVariable<int> _lives;
        private readonly GameManager _gameManager;

        public int CurrentScore { get; private set; }

        public RunScoreMechanics(
            AtomicEvent<ObstacleTypeEnum> collectableCollectedEvent,
            AtomicEvent<Obstacle> destroyObstacleEvent,
            AtomicEvent<Obstacle> destroyObstacleBySuperAttackEvent,
            AtomicVariable<int> lives,
            GameManager gameManager)
        {
            _collectableCollectedEvent = collectableCollectedEvent;
            _destroyObstacleEvent = destroyObstacleEvent;
            _destroyObstacleBySuperAttackEvent =
                destroyObstacleBySuperAttackEvent;
            _lives = lives;
            _gameManager = gameManager;
        }

        public void OnEnable()
        {
            // Сбрасываем score нового забега.
            Reset();

            // Учитываем collectables и разрушения обоих источников.
            _collectableCollectedEvent.Subscribe(OnCollectableCollected);
            _destroyObstacleEvent.Subscribe(OnObstacleDestroyed);
            _destroyObstacleBySuperAttackEvent.Subscribe(OnObstacleDestroyed);
            _gameManager.OnFinish += OnFinish;
        }

        public void OnDisable()
        {
            _collectableCollectedEvent.Unsubscribe(OnCollectableCollected);
            _destroyObstacleEvent.Unsubscribe(OnObstacleDestroyed);
            _destroyObstacleBySuperAttackEvent.Unsubscribe(OnObstacleDestroyed);
            _gameManager.OnFinish -= OnFinish;
        }

        private void Reset()
        {
            CurrentScore = 0;
            Debug.Log("[RunScore] reset score=0");
        }

        private void OnCollectableCollected(ObstacleTypeEnum collectableType)
        {
            switch (collectableType)
            {
                case ObstacleTypeEnum.collectableCoin:
                    AddScore(_coinScore, collectableType.ToString());
                    break;
                case ObstacleTypeEnum.collectableCrystal:
                case ObstacleTypeEnum.collectableEnergetic:
                case ObstacleTypeEnum.collectablePizza:
                case ObstacleTypeEnum.collectableLife:
                    AddScore(_bonusScore, collectableType.ToString());
                    break;
            }
        }

        private void OnObstacleDestroyed(Obstacle obstacle)
        {
            var obstacleName = obstacle == null ? "unknown" : obstacle.name;
            AddScore(_destroyedObstacleScore, $"destroyedObstacle:{obstacleName}");
        }

        private void OnFinish()
        {
            if (_lives.Value > 0)
            {
                Debug.Log($"[RunScore] win final={CurrentScore}");
                return;
            }

            Debug.Log($"[RunScore] discarded score={CurrentScore}");
        }

        private void AddScore(int amount, string source)
        {
            CurrentScore += amount;
            Debug.Log($"[RunScore] add amount={amount} source={source} total={CurrentScore}");
        }
    }
}
