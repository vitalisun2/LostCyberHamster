using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using Atomic.Elements;
using LostCyberHamster.UI;
using UnityEngine;
using Vues.GameCore;

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
        private readonly GameScreenStatusFormatter _statusFormatter = new GameScreenStatusFormatter();
        private int _lastRunScore = -1;
        private bool _wasSkateboardActive;

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
            _wasSkateboardActive = character.ActorSwitcher.IsSkateboardActive;

            _gameScreenController.SetSuperJumpAction(OnSuperJump);
            _gameScreenController.SetJumpAction(OnJump);
            _gameScreenController.SetTapAction(OnTap);
            _gameScreenController.SetPauseAction(OnPause);
            _gameScreenController.SetBuyEnergyAction(OnBuyEnergy);
            _gameScreenController.SetUltraAction(OnUlta);
            _gameScreenController.SetBuyUltraAction(OnBuyUltra);
        }

        public void Subscribe()
        {
            _character?.Lives.Subscribe(OnLifesChanged);
            _character?.Energy.Subscribe(OnEnergyChanged);
            _character?.UltaChargeAmount.Subscribe(
                OnUltaChargeAmountChanged);
        }

        public void SyncState()
        {
            if (_character == null)
            {
                return;
            }

            OnLifesChanged(_character.Lives.Value);
            SyncUltraControls();
            OnEnergyChanged(_character.Energy.Value);
            SyncRunScore();
        }

        public void Unsubscribe()
        {
            _character?.Lives.Unsubscribe(OnLifesChanged);
            _character?.Energy.Unsubscribe(OnEnergyChanged);
            _character?.UltaChargeAmount.Unsubscribe(
                OnUltaChargeAmountChanged);
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
            ResetJumpSequenceIfModeChanged();
            SyncRunScore();

#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            return;
#endif
            if (_statusFormatter.TryFormat(Time.unscaledTime, _gameManager, _character, out var formattedText))
            {
                _gameScreenController.SetHamsterState(formattedText);
            }
        }

        private void SyncRunScore()
        {
            if (_character == null)
            {
                return;
            }

            // Не создаём новую строку каждый кадр, пока счёт не изменился.
            var runScore = _character.RunScore;
            if (runScore == _lastRunScore)
            {
                return;
            }

            _lastRunScore = runScore;
            _gameScreenController?.SetRunScore(runScore);
        }

        private void OnJump()
        {
            if (_character.ActorSwitcher.IsSkateboardActive)
            {
                _jumpEvent?.Invoke();
                return;
            }

            if (_characterHamsterState.Value == HamsterStateEnum.RoofRun)
                _roofJumpRequest.Invoke();

            if (_characterHamsterState.Value == HamsterStateEnum.Run ||
                _character.IsDamaged.Value)
                _jumpEvent?.Invoke();
        }

        private void OnSuperJump()
        {
            if (_character.ActorSwitcher.IsSkateboardActive)
            {
                _superJumpEvent?.Invoke();
                return;
            }

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
                _characterHamsterState.Value == HamsterStateEnum.JumpDamageForSmallAlive ||
                _characterHamsterState.Value == HamsterStateEnum.JumpDamageForSmallNotAlive ||
                _characterHamsterState.Value == HamsterStateEnum.JumpDamageForBigAlive ||
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
            const int price = 50;
            if (ResourceManager.CanSpendResource(ResourceType.Coins, price))
            {
                ResourceManager.SpendResource(ResourceType.Coins, price);
                _character.AddEnergy(100);
            }
        }

        private void OnBuyUltra()
        {
            // Разрешаем покупку заряда только настроенному суперудару.
            if (_character?.HasSuperAttack != true)
            {
                return;
            }

            // Сохраняем прежнее поведение покупки полного заряда.
            const int price = 100;
            if (ResourceManager.CanSpendResource(ResourceType.Coins, price))
            {
                ResourceManager.SpendResource(ResourceType.Coins, price);
                _character?.AddUltaCharge(100);
            }
        }

        private void SyncUltraControls()
        {
            // Показываем элементы только для выбранного суперудара.
            bool hasSuperAttack =
                _character != null &&
                _character.HasSuperAttack;
            _gameScreenController?.SetUltraControlsVisible(
                hasSuperAttack);

            // Синхронизируем текущий заряд после загрузки экрана.
            if (hasSuperAttack)
            {
                OnUltaChargeAmountChanged(
                    _character.UltaChargeAmount.Value);
            }
        }

        private void ResetJumpSequenceIfModeChanged()
        {
            bool isSkateboardActive = _character.ActorSwitcher.IsSkateboardActive;
            if (isSkateboardActive == _wasSkateboardActive)
                return;

            _gameScreenController.ResetJumpSequence();
            _wasSkateboardActive = isSkateboardActive;
        }
    }
}
