using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
using Atomic.Elements;
using UnityEngine;
using Vues.GameCore;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public class AddOneCoinMechanics
    {
        private readonly AtomicEvent _jumpOverEvent;
        private const int _coinValue = 1;

        public AddOneCoinMechanics(AtomicEvent jumpOverEvent)
        {
            _jumpOverEvent = jumpOverEvent;
        }

        public void OnEnable()
        {
            _jumpOverEvent.Subscribe(OnJumpOverEvent);
        }

        public void OnDisable()
        {
            _jumpOverEvent.Unsubscribe(OnJumpOverEvent);
        }

        private void OnJumpOverEvent()
        {
            GameEventsManager.CoinCollected(_coinValue);
            Object.Instantiate(LevelController.Instance.LevelData.CoinOneBonusPrefab);
        }

    }
}
