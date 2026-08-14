using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
using Atomic.Elements;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public class UnspawnOnJumpedOnMechanics
    {
        private readonly Hamster _hamster;
        private readonly Obstacle _obstacleScript;
        private readonly AtomicEvent<Obstacle> _destroyObstacleBySuperAttackEvent;
        private readonly AtomicEvent<GameObject> _onObstacleUnspawn;
        private readonly BoomEffectAction _boomEffectAction;
        private readonly GameManager _gameManager;
        private bool _isEnabled;

        public UnspawnOnJumpedOnMechanics(Obstacle obstacleScript)
        {
            _hamster = obstacleScript.Hamster;
            _obstacleScript = obstacleScript;
            _destroyObstacleBySuperAttackEvent =
                _hamster.DestroyObstacleBySuperAttackEvent;
            _onObstacleUnspawn = obstacleScript.OnObstacleUnspawned;
            _boomEffectAction = obstacleScript.BoomEffectAction;
            _gameManager = obstacleScript.GameManager;
        }

        public void OnEnable()
        {
            if (_isEnabled)
                return;

            _hamster.DestroyObstacleEvent.Subscribe(OnObstacleDestroyed);
            _destroyObstacleBySuperAttackEvent.Subscribe(OnObstacleDestroyed);
            _isEnabled = true;
        }

        public void OnDisable()
        {
            if (!_isEnabled)
                return;

            _hamster.DestroyObstacleEvent.Unsubscribe(OnObstacleDestroyed);
            _destroyObstacleBySuperAttackEvent.Unsubscribe(OnObstacleDestroyed);
            _isEnabled = false;
        }

        private void OnObstacleDestroyed(Obstacle destroyedObstacle)
        {
            if(destroyedObstacle != _obstacleScript)
                return;

            _onObstacleUnspawn.Invoke(_obstacleScript.gameObject);
            _boomEffectAction.Invoke(_obstacleScript.transform.position, _gameManager);
        }
    }
}
