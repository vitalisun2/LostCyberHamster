using Atomic.Elements;
using System;
using System.Linq;
using Assets.Scripts.Gameplay;
using UnityEngine.UIElements;
using LostCyberHamster.UI;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public class HealthBarMechanics
    {
        private Healthbar _healthbar;
        private readonly AtomicVariable<int> _lives;
        private readonly AtomicEvent _characterDeathEvent;

        public HealthBarMechanics(Healthbar healthBar, AtomicVariable<int> lives, AtomicEvent characterDeathEvent)
        {
            _healthbar = healthBar;
            _lives = lives;
            _characterDeathEvent = characterDeathEvent;
        }

        public void Subscribe()
        {
            _lives.Subscribe(OnLifesChanged);
        }

        public void Unsubscribe()
        {
            _lives.Unsubscribe(OnLifesChanged);
        }

        private void OnLifesChanged(int lives)
        {
            _healthbar.value = lives;

            Debug.Log("Lives: " + lives);

            if (lives == 0)
            {
                _characterDeathEvent.Invoke();
            }
        }
    }
}
