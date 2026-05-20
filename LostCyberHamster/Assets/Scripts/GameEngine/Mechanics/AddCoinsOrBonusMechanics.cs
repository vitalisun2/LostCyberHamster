using System;
using System.Collections;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
using Atomic.Elements;
using UnityEngine;
using Vues.GameCore;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public class AddCoinsOrBonusMechanics
    {
        private readonly AtomicEvent<Obstacle> _destroyObstacleEvent;
        private readonly MonoBehaviour _monoBehaviour;

        private const float _coinSpacing = 0.2f;
        private const int _coinValue = 3;
        private const float _delayBetweenCoinsSec = 0.2f;
        private const int _energyBonusValue = 20;
        private const int _crystallBonusValue = 1;

        public AddCoinsOrBonusMechanics(AtomicEvent<Obstacle> destroyObstacleEvent, MonoBehaviour monoBehaviour)
        {
            _destroyObstacleEvent = destroyObstacleEvent;
            _monoBehaviour = monoBehaviour;
        }

        public void OnEnable()
        {
            _destroyObstacleEvent.Subscribe(OnJumpOnEvent);
        }

        public void OnDisable()
        {
            _destroyObstacleEvent.Unsubscribe(OnJumpOnEvent);
        }

        private void OnJumpOnEvent(Obstacle destroyedObstacle)
        {
            CalculateAndApplyBonus();
        }

        private void CalculateAndApplyBonus()
        {
            float bonusChance = Random.value;

            if (bonusChance < 0.3f)
            {
                // 30% chance to get a bonus
                float bonusTypeChance = Random.value;

                if (bonusTypeChance < 0.85f)
                {
                    // 85% chance to get an energy bonus
                    ApplyEnergyBonus();
                }
                else if (bonusTypeChance < 0.9f)
                {
                    // Next 5% (from 85% to 90%) chance to get a life bonus
                    ApplyLifeBonus();
                }
                else
                {
                    // Remaining 10% chance to get a crystals bonus
                    ApplyCrystalsBonus();
                }
            }
            else
            {
                // 70% chance to get 3 coins
                Apply3CoinsBonus();
            }
        }

        private void ApplyCrystalsBonus()
        {
            GameEventsManager.CrystallCollected(_crystallBonusValue);
            Object.Instantiate(LevelController.Instance.LevelData.CrystalBonusPrefab);
        }

        private void ApplyLifeBonus()
        {
            var hamster = LevelController.Instance.LevelData.Hamster;
            var livesToAdd = Mathf.Min(3 - hamster.Lives.Value, 1);
            
            hamster.Lives.Value += livesToAdd;
            if(livesToAdd > 0){
                GameEventsManager.LivesAdded(livesToAdd);
            }

            Object.Instantiate(LevelController.Instance.LevelData.LifeBonusPrefab);
        }

        private void ApplyEnergyBonus()
        {
            // setting energy bonus
            var hamster = LevelController.Instance.LevelData.Hamster;
            var energyToAdd = Mathf.Min(100 - hamster.Energy.Value, _energyBonusValue);
            hamster.Energy.Value += energyToAdd;
            if(energyToAdd > 0){
                GameEventsManager.EnergyAdded(energyToAdd);
            }


            // instantiating bonus effect
            float bonusTypeChance = Random.value;

            BaseEffect bonusPrefab = bonusTypeChance <= 0.5f
                ? LevelController.Instance.LevelData.EnergeticBonusPrefab
                : LevelController.Instance.LevelData.PizzaBonusPrefab;
            
            Object.Instantiate(bonusPrefab);
        }

        private void Apply3CoinsBonus()
        {
            _monoBehaviour.StartCoroutine(Apply3CoinsCoroutine());
        }

        private IEnumerator Apply3CoinsCoroutine()
        {
            GameEventsManager.CoinCollected(_coinValue);

            var coinPrefab = LevelController.Instance.LevelData.CoinOneBonusPrefab;

            Object.Instantiate(coinPrefab, coinPrefab.transform.position, Quaternion.identity);

            yield return new WaitForSeconds(_delayBetweenCoinsSec);

            Object.Instantiate(coinPrefab, coinPrefab.transform.position + new Vector3(-_coinSpacing, 0, 0), Quaternion.identity);

            yield return new WaitForSeconds(_delayBetweenCoinsSec);

            Object.Instantiate(coinPrefab, coinPrefab.transform.position + new Vector3(_coinSpacing, 0, 0), Quaternion.identity);
        }
    }
}
