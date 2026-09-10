using System;
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
        public const int CoinScore = 1;
        public const int BonusScore = 2;
        public const int DestroyedObstacleScore = 3;

        private readonly AtomicEvent<ObstacleTypeEnum> _collectableCollectedEvent;
        private readonly AtomicEvent<Obstacle> _destroyObstacleEvent;
        private readonly AtomicEvent<Obstacle> _destroyObstacleBySuperAttackEvent;
        private readonly AtomicVariable<int> _lives;
        private readonly GameManager _gameManager;

        public int CurrentScore { get; private set; }
        public event Action<int, int> ScoreChanged;

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
            // Экземпляр механики принадлежит попытке; повторное включение при revive сохраняет score.
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

        private void OnCollectableCollected(ObstacleTypeEnum collectableType)
        {
            switch (collectableType)
            {
                case ObstacleTypeEnum.collectableCoin:
                    AddScore(CoinScore, collectableType.ToString());
                    break;
                case ObstacleTypeEnum.collectableCrystal:
                case ObstacleTypeEnum.collectableEnergetic:
                case ObstacleTypeEnum.collectablePizza:
                case ObstacleTypeEnum.collectableLife:
                    AddScore(BonusScore, collectableType.ToString());
                    break;
            }
        }

        private void OnObstacleDestroyed(Obstacle obstacle)
        {
            var obstacleName = obstacle == null ? "unknown" : obstacle.name;
            AddScore(DestroyedObstacleScore, $"destroyedObstacle:{obstacleName}");
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
            var previous = CurrentScore;
            CurrentScore += amount;
            ScoreChanged?.Invoke(previous, CurrentScore);
            Debug.Log($"[RunScore] add amount={amount} source={source} total={CurrentScore}");
        }
    }
}
