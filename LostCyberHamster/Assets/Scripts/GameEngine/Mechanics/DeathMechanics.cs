using Assets.Scripts.System;
using Atomic.Elements;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public class DeathMechanics
    {
        private readonly AtomicVariable<int> _lives;

        public DeathMechanics(AtomicVariable<int> lives)
        {
            _lives = lives;
        }

        public void OnEnable()
        {
            _lives.Subscribe(OnLivesChanged);
        }

        public void OnDisable()
        {
            _lives.Unsubscribe(OnLivesChanged);
        }

        private void OnLivesChanged(int lives)
        {
            if (lives == 0)
            {
                LevelController.Instance.Finish();
            }
        }
    }
}
