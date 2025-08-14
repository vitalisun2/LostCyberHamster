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
        private readonly AtomicEvent<GameObject> _onObstacleUnspawn;
        private readonly BoomEffectAction _boomEffectAction;
        private readonly GameManager _gameManager;

        public UnspawnOnJumpedOnMechanics(Obstacle obstacleScript)
        {
            _hamster = obstacleScript.Hamster;
            _obstacleScript = obstacleScript;
            _onObstacleUnspawn = obstacleScript.OnObstacleUnspawned;
            _boomEffectAction = obstacleScript.BoomEffectAction;
            _gameManager = obstacleScript.GameManager;
        }

        public void OnEnable()
        {
            _hamster.DestroyObstacleEvent.Subscribe(OnJumpedOn);
        }

        public void OnDisable()
        {
            _hamster.DestroyObstacleEvent.Unsubscribe(OnJumpedOn);
        }

        private void OnJumpedOn()
        {
            if(_hamster.LastObstacle.Value != _obstacleScript)
                return;

            _onObstacleUnspawn.Invoke(_obstacleScript.gameObject);
            _boomEffectAction.Invoke(_obstacleScript.transform.position, _gameManager);
        }
    }
}
