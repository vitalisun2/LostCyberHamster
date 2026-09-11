using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using System;
using Assets.Scripts.System;
using Assets.Scripts.Tutorial;
using LostCyberHamster.UI;
using UnityEngine;
using Vues.GameCore;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public class UiGameScreenMechanics
    {
        private const int EnergyRefillPrice = 50;
        private const int UltraRefillPrice = 100;
        private readonly RunCoinBudget _runCoinBudget = new();
        public int GrossCollectedCoins => _runCoinBudget.GrossCoins;
        private bool _refillInProgress;
        private readonly UIManager _uiManager;
        private readonly GameManager _gameManager;
        private readonly Hamster _character;
        private readonly PlayerJumpInputSequencer _jumpInputSequencer;
        private GameScreenController _gameScreenController;
        private readonly GameScreenStatusFormatter _statusFormatter = new GameScreenStatusFormatter();
        private int _lastRunScore = -1;
        private int _runCrystals;

        public UiGameScreenMechanics(
            UIManager uiManager,
            GameManager gameManager,
            Hamster character,
            PlayerJumpInputSequencer jumpInputSequencer)
        {
            _uiManager = uiManager;
            _gameManager = gameManager;
            _character = character;
            _jumpInputSequencer = jumpInputSequencer;

            _gameScreenController = _uiManager.GetController<GameScreenController>();

            _gameScreenController.SetJumpInputAction(OnJumpInput);
            _gameScreenController.SetTapAction(OnTap);
            _gameScreenController.SetBuyEnergyAction(OnBuyEnergy);
            _gameScreenController.SetUltraAction(OnUlta);
            _gameScreenController.SetBuyUltraAction(OnBuyUltra);
            _gameScreenController.SetRefillPrices(EnergyRefillPrice, UltraRefillPrice);
        }

        public void Subscribe()
        {
            // Подключаем показатели персонажа.
            _character?.Lives.Subscribe(OnLifesChanged);
            _character?.Energy.Subscribe(OnEnergyChanged);
            _character?.UltaChargeAmount.Subscribe(
                OnUltaChargeAmountChanged);

            // Слушаем добычу уровня в течение жизни игровой сцены, включая паузу HUD.
            GameEventsManager.OnCoinCollected += OnCoinCollected;
            GameEventsManager.OnCrystalsCollected += OnCrystalsCollected;
            ResourceManager.BalanceChanged += OnBalanceChanged;
        }

        public void SyncState()
        {
            if (_character == null)
            {
                return;
            }

            // Восстанавливаем показатели после загрузки дерева HUD.
            OnLifesChanged(_character.Lives.Value);
            _gameScreenController.SetAbilityActivity(_character.SuperAttackSnapshot);
            SyncUltraControls();
            OnEnergyChanged(_character.Energy.Value);
            SyncRunScore();
            SyncRunResources();
            SyncRefillAvailability();
        }

        public void Unsubscribe()
        {
            // Освобождаем подписки персонажа.
            _character?.Lives.Unsubscribe(OnLifesChanged);
            _character?.Energy.Unsubscribe(OnEnergyChanged);
            _character?.UltaChargeAmount.Unsubscribe(
                OnUltaChargeAmountChanged);
            // Завершаем отслеживание добычи уходящего уровня.
            GameEventsManager.OnCoinCollected -= OnCoinCollected;
            GameEventsManager.OnCrystalsCollected -= OnCrystalsCollected;
            ResourceManager.BalanceChanged -= OnBalanceChanged;
        }

        private void OnCoinCollected(int amount)
        {
            _runCoinBudget.RecordCollection(amount);
            SyncRunResources();
            SyncRefillAvailability();
        }

        private void OnCrystalsCollected(int amount)
        {
            _runCrystals = AddCollectedAmount(_runCrystals, amount);
            SyncRunResources();
        }

        private void SyncRunResources() =>
            _gameScreenController?.SetRunResources(_runCoinBudget.AvailableCoins, _runCrystals);

        private void OnBalanceChanged(ResourceType resource, int balance)
        {
            if (resource != ResourceType.Coins) return;
            SyncRunResources();
            SyncRefillAvailability();
        }

        /// <summary>Суммирует положительную добычу без переполнения счётчика.</summary>
        private static int AddCollectedAmount(int current, int amount)
        {
            return current + Mathf.Clamp(amount, 0, int.MaxValue - current);
        }

        private void OnLifesChanged(int lives)
        {
            _gameScreenController?.SetHealth(lives);
        }

        private void OnUltaChargeAmountChanged(int value)
        {
            _gameScreenController?.SetUltraValue(value);
            SyncRefillAvailability();
        }

        private void OnEnergyChanged(int energy)
        {
            _gameScreenController?.SetEnergy(energy);
            SyncRefillAvailability();
        }

        public void OnUpdate()
        {
            _gameScreenController.SetAbilityActivity(_character.SuperAttackSnapshot);
            SyncRunScore();
            SyncRefillAvailability();

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

        private void OnJumpInput()
        {
            _jumpInputSequencer.HandleJumpInput();
        }

        private void OnTap()
        {
            _character.TapRequest?.Invoke();
        }

        private void OnUlta()
        {
            _character.UltaEvent?.Invoke();
        }

        private bool CanBuyRefill(bool ultra)
        {
            if (_refillInProgress || _character == null || _gameManager.State != GameState.PLAYING ||
                GameplayInputGate.IsBlocked || _uiManager.HasModalOrTransition ||
                TutorialStorage.IsPlayerDataBackupActive) return false;
            bool hasRoom = ultra ? _character.HasSuperAttack && _character.UltaChargeAmount.Value < 100
                : _character.Energy.Value < 100;
            return hasRoom && _runCoinBudget.CanSpend(ultra ? UltraRefillPrice : EnergyRefillPrice);
        }

        private void SyncRefillAvailability() =>
            _gameScreenController?.SetRefillAvailability(CanBuyRefill(false), CanBuyRefill(true));

        private void OnBuyEnergy() => BuyRefill(ultra: false);
        private void OnBuyUltra() => BuyRefill(ultra: true);

        /// <summary>Повторно проверяет шкалу и оплачивает пополнение до изменения ресурсов персонажа.</summary>
        private void BuyRefill(bool ultra)
        {
            if (!CanBuyRefill(ultra)) return;
            _refillInProgress = true;
            try
            {
                // После сохранения расхода уменьшаем HUD-остаток; квестовая добыча остаётся валовой.
                if (!_runCoinBudget.TrySpend(ultra ? UltraRefillPrice : EnergyRefillPrice)) return;
                if (ultra) _character.AddUltaCharge(100);
                else _character.AddEnergy(100);
                SyncRunResources();
                ResourceManager.NotifyBalancesChangedAfterCommit();
            }
            catch (Exception exception) { Debug.LogException(exception); }
            finally
            {
                _refillInProgress = false;
                SyncRefillAvailability();
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
    }
}
