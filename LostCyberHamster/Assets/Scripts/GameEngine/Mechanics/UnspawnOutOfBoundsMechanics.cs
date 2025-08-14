using Assets.Scripts.Gameplay;
using Atomic.Elements;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public class UnspawnOutOfBoundsMechanics
    {
        private readonly Obstacle _obstacleScript;
        private readonly AtomicEvent<GameObject> _onObstacleUnspawn;
        private readonly float _lowerBound = -15;

        public UnspawnOutOfBoundsMechanics(Obstacle obstacleScript, AtomicEvent<GameObject> onObstacleUnspawn)
        {
            _obstacleScript = obstacleScript;
            _onObstacleUnspawn = onObstacleUnspawn;
        }

        public void Update()
        {
            if (_obstacleScript.transform.position.x < _lowerBound)
            {
                _onObstacleUnspawn.Invoke(_obstacleScript.gameObject);
            }
        }
    }
}
