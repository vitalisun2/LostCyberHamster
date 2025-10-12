using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using Atomic.Elements;
using LostCyberHamster.UI;
using UnityEngine;
using Vues.GameCore;
using NotImplementedException = System.NotImplementedException;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public class UiGameScreenMechanics
    {
        private readonly UIManager _uiManager;
        private readonly GameManager _gameManager;
        private readonly Hamster _character;
        private GameScreenController _gameScreenController;
        private readonly AtomicVariable<HamsterStateEnum> _characterHamsterState;
        private readonly AtomicEvent _roofJumpRequest;
        private readonly AtomicEvent _superRoofJumpRequest;
        private readonly AtomicEvent _jumpEvent;
        private readonly AtomicEvent _superJumpEvent;

        public UiGameScreenMechanics(UIManager uiManager, GameManager gameManager, Hamster character)
        {
            _uiManager = uiManager;
            _gameManager = gameManager;
            _character = character;
            _characterHamsterState = character.HamsterState;
            _roofJumpRequest = character.RoofJumpRequest;
            _superRoofJumpRequest = character.SuperRoofJumpRequest;
            _jumpEvent = character.JumpRequest;
            _superJumpEvent = character.SuperJumpRequest;

            _gameScreenController = _uiManager.GetController<GameScreenController>();

            _gameScreenController.SetSuperJumpAction(OnSuperJump);
            _gameScreenController.SetJumpAction(OnJump);
            _gameScreenController.SetTapAction(OnTap);
            _gameScreenController.SetUltraAction(OnUlta);
            _gameScreenController.SetPauseAction(OnPause);
            _gameScreenController.SetBuyEnergyAction(OnBuyEnergy);
            _gameScreenController.SetBuyUltraAction(OnBuyUltra);
        }

        public void Subscribe()
        {
            _character?.Lives.Subscribe(OnLifesChanged);
            _character?.UltaChargeAmount.Subscribe(OnUltaChargeAmountChanged);
            _character?.Energy.Subscribe(OnEnergyChanged);
        }

        public void Unsubscribe()
        {
            _character?.Lives.Unsubscribe(OnLifesChanged);
            _character?.UltaChargeAmount.Unsubscribe(OnUltaChargeAmountChanged);
            _character?.Energy.Unsubscribe(OnEnergyChanged);
        }

        private void OnLifesChanged(int lives)
        {
            _gameScreenController?.SetHealth(lives);
        }

        private void OnUltaChargeAmountChanged(int value)
        {
            _gameScreenController?.SetUltraValue(value);
        }

        private void OnEnergyChanged(int energy)
        {
            _gameScreenController?.SetEnergy(energy);
        }

        public void OnUpdate()
        {
            var patternIndex = GetDisplayedPatternIndex();
            var patternName = GetDisplayedPatternName(patternIndex);
            var patternIndexText = patternIndex >= 0 ? patternIndex.ToString() : "-";
            var patternNameText = string.IsNullOrEmpty(patternName) ? "-" : patternName;

            var outputStr =
                $"" +
                $"{LevelManager.GetLocationName()} " +
                $"{LevelManager.GetCurrentPartOfDay()}, " +
                $"{patternNameText} {patternIndexText}, " +
                $"{_gameManager.State.ToString()},\n " +
                $"{_character.HamsterState.Value}, isDamaged: {_character.IsDamaged.Value}";

            _gameScreenController.SetHamsterState(outputStr);
        }

        private void OnJump()
        {
            if (_characterHamsterState.Value == HamsterStateEnum.RoofRun)
                _roofJumpRequest.Invoke();

            if (_characterHamsterState.Value == HamsterStateEnum.Run ||
                _character.IsDamaged.Value)
                _jumpEvent?.Invoke();
        }

        private void OnSuperJump()
        {
            if (_characterHamsterState.Value == HamsterStateEnum.RoofJump ||
                _characterHamsterState.Value == HamsterStateEnum.JumpFromRoof ||
                _characterHamsterState.Value == HamsterStateEnum.JumpFromRoofDamage ||
                _characterHamsterState.Value == HamsterStateEnum.JumpOnObstacleFromRoof
               )
                _superRoofJumpRequest.Invoke();

            if (_characterHamsterState.Value == HamsterStateEnum.Jump ||
                _characterHamsterState.Value == HamsterStateEnum.JumpOver ||
                _characterHamsterState.Value == HamsterStateEnum.JumpOnObstacle ||
                _characterHamsterState.Value == HamsterStateEnum.JumpOnRoof ||
                _characterHamsterState.Value == HamsterStateEnum.JumpDamageForSmallNotAlive ||
                _characterHamsterState.Value == HamsterStateEnum.JumpOnRoofDamage
               )
            {
                _superJumpEvent?.Invoke();
            }
        }

        private void OnTap()
        {
            _character.TapRequest?.Invoke();
        }

        private void OnUlta()
        {
            _character.UltaEvent?.Invoke();
        }

        private void OnPause()
        {
            _gameManager.Pause();
        }

        private void OnBuyEnergy()
        {
            var price = 50;
            if (ResourceManager.CanSpendResource(ResourceType.Coins, price))
            {
                ResourceManager.SpendResource(ResourceType.Coins, price);
                _character.AddEnergy(100);
            }
        }

        private void OnBuyUltra()
        {
            var price = 100;
            if (ResourceManager.CanSpendResource(ResourceType.Coins, price))
            {
                ResourceManager.SpendResource(ResourceType.Coins, price);
                _character.AddUltaCharge(100);
            }
        }

        private int GetDisplayedPatternIndex()
        {
            var spawner = ObstacleSpawner.Instance;
            if (spawner == null)
            {
                return -1;
            }

            var levelController = LevelController.Instance;
            if (levelController?.IsLevelLoaded != true)
            {
                return -1;
            }

            var index = spawner.CurrPatternIndex - 1;
            return index >= 0 ? index : -1;
        }

        private string GetDisplayedPatternName(int patternIndex)
        {
            if (patternIndex < 0)
            {
                return string.Empty;
            }

            var levelInfo = LevelController.Instance?.LevelData?.LevelInfo;
            var patterns = levelInfo?.patterns;
            if (patterns == null || patternIndex >= patterns.Count)
            {
                return string.Empty;
            }

            return patterns[patternIndex]?.name ?? string.Empty;
        }
    }
}
