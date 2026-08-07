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
            _hamster.DestroyObstacleEvent.Subscribe(OnObstacleDestroyed);
            _destroyObstacleBySuperAttackEvent.Subscribe(OnObstacleDestroyed);
        }

        public void OnDisable()
        {
            _hamster.DestroyObstacleEvent.Unsubscribe(OnObstacleDestroyed);
            _destroyObstacleBySuperAttackEvent.Unsubscribe(OnObstacleDestroyed);
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
