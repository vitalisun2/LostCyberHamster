using System;
using Assets.Scripts.GameEngine.Actors;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using UnityEngine;

namespace Vues.GameCore
{
    /// <summary>
    /// Владеет lifecycle skateboard mode и таймером ожидания первого прыжка.
    /// </summary>
    public sealed class SkateboardAttack : ISuperAttackRuntime
    {
        public const float DefaultFirstJumpTimeout = 10f;
        public const int DefaultChargePerObstacle = 20;

        private readonly Hamster _hamster;
        private readonly HamsterActorSwitcher _actorSwitcher;
        private readonly GameManager _gameManager;
        private readonly float _firstJumpTimeout;

        private float _firstJumpTimeLeft;
        private bool _isWaitingForFirstJump;
        private bool _isActive;
        private bool _isDisposed;

        /// <summary>
        /// Возвращает заряд за одно уничтоженное препятствие.
        /// </summary>
        public int ChargePerObstacle { get; }

        /// <summary>
        /// Возвращает признак активного skateboard mode.
        /// </summary>
        public bool IsActive => _isActive;

        /// <summary>
        /// Возвращает признак работающего таймера до первого прыжка.
        /// </summary>
        public bool IsWaitingForFirstJump => _isWaitingForFirstJump;

        /// <summary>
        /// Создаёт runtime с явными зависимостями Hamster и текущего GameManager.
        /// </summary>
        public SkateboardAttack(
            Hamster hamster,
            GameManager gameManager,
            float firstJumpTimeout = DefaultFirstJumpTimeout,
            int chargePerObstacle = DefaultChargePerObstacle)
        {
            _hamster = hamster ?? throw new ArgumentNullException(nameof(hamster));
            _actorSwitcher = hamster.ActorSwitcher ??
                throw new MissingReferenceException("HamsterActorSwitcher is missing.");
            _gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));

            if (firstJumpTimeout <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(firstJumpTimeout),
                    firstJumpTimeout,
                    "First jump timeout must be positive.");
            }

            if (chargePerObstacle <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(chargePerObstacle),
                    chargePerObstacle,
                    "Charge per obstacle must be positive.");
            }

            _firstJumpTimeout = firstJumpTimeout;
            ChargePerObstacle = chargePerObstacle;
            _gameManager.OnFinish += OnGameFinished;
        }

        /// <summary>
        /// Включает skateboard mode только из обычного живого Run во время gameplay.
        /// </summary>
        public bool TryActivate()
        {
            if (_isDisposed ||
                _isActive ||
                _gameManager.State != GameState.PLAYING ||
                _hamster.HamsterState.Value != HamsterStateEnum.Run ||
                _hamster.IsDamaged.Value ||
                _actorSwitcher.IsSkateboardActive)
            {
                return false;
            }

            _firstJumpTimeLeft = _firstJumpTimeout;
            _isWaitingForFirstJump = true;
            _actorSwitcher.ActivateSkateboard();
            _isActive = true;
            return true;
        }

        /// <summary>
        /// Останавливает начальный timeout после первого принятого skateboard jump.
        /// </summary>
        public bool NotifyFirstJumpStarted()
        {
            if (!_isActive || !_isWaitingForFirstJump)
                return false;

            _isWaitingForFirstJump = false;
            _firstJumpTimeLeft = 0f;
            return true;
        }

        /// <summary>
        /// Завершает skateboard mode после timeout или будущего расходования прыжков.
        /// </summary>
        public void Complete()
        {
            if (_isDisposed)
                return;

            Deactivate();
        }

        /// <summary>
        /// Обновляет только gameplay-time timeout ожидания первого прыжка.
        /// </summary>
        public void Update()
        {
            if (_isDisposed || !_isActive)
                return;

            // Finish завершает режим сразу; pause оставляет timeout без изменений.
            if (_gameManager.State == GameState.FINISHED)
            {
                Deactivate();
                return;
            }

            if (_gameManager.State != GameState.PLAYING || !_isWaitingForFirstJump)
                return;

            // До первого прыжка режим живёт не больше заданного gameplay-time.
            _firstJumpTimeLeft -= Time.deltaTime;
            if (_firstJumpTimeLeft <= 0f)
                Deactivate();
        }

        /// <summary>
        /// Отписывает runtime и гарантированно возвращает normal actor.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _gameManager.OnFinish -= OnGameFinished;
            Deactivate();
            _isDisposed = true;
        }

        private void OnGameFinished()
        {
            Deactivate();
        }

        private void Deactivate()
        {
            _isActive = false;
            _isWaitingForFirstJump = false;
            _firstJumpTimeLeft = 0f;
            _actorSwitcher.ActivateNormal();
        }
    }
}
