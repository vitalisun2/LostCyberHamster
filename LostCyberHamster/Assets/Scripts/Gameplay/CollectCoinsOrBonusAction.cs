using Assets.Scripts.Common.Models;
using Assets.Scripts.System;
using Atomic.Elements;
using UnityEngine;
using Vues.GameCore;

namespace Assets.Scripts.Gameplay
{
    public class CollectCoinsOrBonusAction: IAtomicAction<Obstacle>
    {
        private readonly Hamster _hamster;

        public CollectCoinsOrBonusAction(Hamster hamster)
        {
            _hamster = hamster;
        }

        public void Invoke(Obstacle obstacle)
        {
            switch (obstacle.ObstacleType.ObstacleTypeEnum)
            {
                case ObstacleTypeEnum.collectableCoin:
                    CollectCoin();
                    break;
                case ObstacleTypeEnum.collectableCrystal:
                    CollectCrystal();
                    break;
                case ObstacleTypeEnum.collectableEnergetic:
                    CollectEnergetic();
                    break;
                case ObstacleTypeEnum.collectablePizza:
                    CollectPizza();
                    break;
                case ObstacleTypeEnum.collectableLife:
                    CollectLife();
                    break;
            }
        }

        private void CollectCoin()
        {
            GameEventsManager.CrystallCollected(1);
            Object.Instantiate(
                LevelController.Instance.LevelData.CoinOneBonusPrefab, _hamster.EffectsSlot.transform.position, Quaternion.identity);
        }

        private void CollectCrystal()
        {
            GameEventsManager.CrystallCollected(1);
            Object.Instantiate(
                LevelController.Instance.LevelData.CrystalBonusPrefab, _hamster.EffectsSlot.transform.position, Quaternion.identity);
        }

        private void CollectEnergetic()
        {
            _hamster.AddEnergy();
            Object.Instantiate(
                LevelController.Instance.LevelData.EnergeticBonusPrefab, _hamster.EffectsSlot.transform.position, Quaternion.identity);
        }

        private void CollectPizza()
        {
            _hamster.AddEnergy();
            Object.Instantiate(
                LevelController.Instance.LevelData.PizzaBonusPrefab, _hamster.EffectsSlot.transform.position, Quaternion.identity);
        }

        private void CollectLife()
        {
            var livesToAdd = Mathf.Min(3 - _hamster.Lives.Value, 1);
            _hamster.Lives.Value += livesToAdd;
            if (livesToAdd > 0){
                GameEventsManager.LivesAdded(livesToAdd);
            }

            Object.Instantiate(
                LevelController.Instance.LevelData.LifeBonusPrefab, _hamster.EffectsSlot.transform.position, Quaternion.identity);
        }
    }
}
