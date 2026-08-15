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

            _hamster.DestroyObstacleEvent.Subscribe(OnObstacleDestroyedByNormalJump);
            _destroyObstacleBySuperAttackEvent.Subscribe(OnObstacleDestroyedBySuperAttack);
            _isEnabled = true;
        }

        public void OnDisable()
        {
            if (!_isEnabled)
                return;

            _hamster.DestroyObstacleEvent.Unsubscribe(OnObstacleDestroyedByNormalJump);
            _destroyObstacleBySuperAttackEvent.Unsubscribe(OnObstacleDestroyedBySuperAttack);
            _isEnabled = false;
        }

        private void OnObstacleDestroyedByNormalJump(Obstacle destroyedObstacle)
        {
            OnObstacleDestroyed(destroyedObstacle);
        }

        private void OnObstacleDestroyedBySuperAttack(Obstacle destroyedObstacle)
        {
            OnObstacleDestroyed(destroyedObstacle);
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
