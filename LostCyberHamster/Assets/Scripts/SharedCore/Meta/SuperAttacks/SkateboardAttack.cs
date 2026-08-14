using System;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Actors;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.GameEngine.Skins;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using UnityEngine;

namespace Vues.GameCore
{
    /// <summary>
    /// Управляет lifecycle, прыжками, combo и visual-состоянием skateboard mode.
    /// </summary>
    public sealed class SkateboardAttack : ISuperAttackRuntime
    {
        public const float DefaultFirstJumpTimeout = 10f;
        public const float DefaultJumpDuration = 1.25f;
        public const float DefaultLandingContactTime = 10f / 12f;
        public const int DefaultJumpBudget = 3;
        public const int DefaultChargePerObstacle = 20;

        private const float _rideVisualDuration = 8f / 12f;
        private const float _pushVisualDuration = 11f / 12f;

        private readonly Hamster _hamster;
        private readonly HamsterActorSwitcher _actorSwitcher;
        private readonly SkateboardSurfaceController _surfaceController;
        private readonly SkinVisualHost _visualHost;
        private readonly GameManager _gameManager;
        private readonly ICameraShake _cameraShake;
        private readonly SkateboardLandingImpactMechanics _landingImpactMechanics;
        private readonly float _firstJumpTimeout;
        private readonly float _jumpDuration;
        private readonly float _landingContactTime;
        private readonly int _jumpBudget;

        private float _firstJumpTimeLeft;
        private float _stateTimeLeft;
        private float _rideVisualTimeLeft;
        private long _nextActionId;
        private long _currentJumpActionId;
        private int _rideVisualIndex;
        private int _jumpsRemaining;
        private int _comboDepth;
        private bool _isJumpQueued;
        private bool _isQueuedJumpSuper;
        private bool _isWaitingForFirstJump;
        private bool _isVisualPlaybackEnabled;
        private bool _isActive;
        private bool _isDisposed;
        private SkateboardState _state;

        /// <summary>
        /// Сообщает уровень combo в момент контакта skateboard с землёй.
        /// </summary>
        public event Action<int> LandingImpact;

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
        /// Возвращает число ещё доступных отдельных прыжков.
        /// </summary>
        public int JumpsRemaining => _jumpsRemaining;

        /// <summary>
        /// Возвращает глубину текущей непрерывной серии прыжков.
        /// </summary>
        public int ComboDepth => _comboDepth;

        /// <summary>
        /// Возвращает признак ride-состояния skateboard mode.
        /// </summary>
        public bool IsRiding => _state == SkateboardState.Ride;

        /// <summary>
        /// Возвращает признак обычного или усиленного skateboard jump.
        /// </summary>
        public bool IsJumping =>
            _state is SkateboardState.Jump or SkateboardState.SuperJump;

        /// <summary>
        /// Возвращает признак усиленного skateboard jump.
        /// </summary>
        public bool IsSuperJumping => _state == SkateboardState.SuperJump;

        /// <summary>
        /// Возвращает признак landing tail после контакта с землёй.
        /// </summary>
        public bool IsLanding => _state == SkateboardState.Landing;

        /// <summary>
        /// Создаёт runtime с явными gameplay, visual и surface-зависимостями.
        /// </summary>
        public SkateboardAttack(
            Hamster hamster,
            GameManager gameManager,
            Camera camera,
            float firstJumpTimeout = DefaultFirstJumpTimeout,
            int chargePerObstacle = DefaultChargePerObstacle,
            float jumpDuration = DefaultJumpDuration,
            float landingContactTime = DefaultLandingContactTime,
            int jumpBudget = DefaultJumpBudget)
        {
            _hamster = hamster ?? throw new ArgumentNullException(nameof(hamster));
            _actorSwitcher = hamster.ActorSwitcher ??
                throw new MissingReferenceException("HamsterActorSwitcher is missing.");
            _surfaceController = hamster.SkateboardSurfaceController ??
                throw new MissingReferenceException("SkateboardSurfaceController is missing.");
            _visualHost = hamster.SkateboardSkinVisualHost ??
                throw new MissingReferenceException("Skateboard SkinVisualHost is missing.");
            _gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
            _cameraShake = new CameraShakeController(
                camera ?? throw new ArgumentNullException(nameof(camera)));

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

            if (jumpDuration <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(jumpDuration),
                    jumpDuration,
                    "Jump duration must be positive.");
            }

            if (landingContactTime <= 0f || landingContactTime >= jumpDuration)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(landingContactTime),
                    landingContactTime,
                    "Landing contact time must be inside jump duration.");
            }

            if (jumpBudget <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(jumpBudget),
                    jumpBudget,
                    "Jump budget must be positive.");
            }

            _firstJumpTimeout = firstJumpTimeout;
            _jumpDuration = jumpDuration;
            _landingContactTime = landingContactTime;
            _jumpBudget = jumpBudget;
            ChargePerObstacle = chargePerObstacle;
            _landingImpactMechanics = new SkateboardLandingImpactMechanics(
                _hamster,
                this,
                _gameManager,
                camera,
                _cameraShake);
            _gameManager.OnFinish += OnGameFinished;
            _hamster.JumpRequest.Subscribe(OnJumpRequested);
            _hamster.RoofJumpRequest.Subscribe(OnJumpRequested);
            _hamster.SuperJumpRequest.Subscribe(OnSuperJumpRequested);
            _hamster.SuperRoofJumpRequest.Subscribe(OnSuperJumpRequested);
            _hamster.DamageEvent.Subscribe(OnDamageReceived);
        }

        /// <summary>
        /// Включает skateboard mode с разрешённой стабильной поверхности во время gameplay.
        /// </summary>
        public bool TryActivate()
        {
            if (_isDisposed ||
                _isActive ||
                _gameManager.State != GameState.PLAYING ||
                !CanActivateFromCurrentSurface() ||
                _hamster.IsDamaged.Value ||
                _actorSwitcher.IsSkateboardActive)
            {
                return false;
            }

            // Инициализируем новый mode lease и включаем отдельный actor целиком.
            _firstJumpTimeLeft = _firstJumpTimeout;
            _isWaitingForFirstJump = true;
            _jumpsRemaining = _jumpBudget;
            _comboDepth = 0;
            _rideVisualIndex = 0;
            _isJumpQueued = false;
            _isQueuedJumpSuper = false;
            _actorSwitcher.ActivateSkateboard();
            _visualHost.Rebind();
            SetVisualPlaybackEnabled(isEnabled: true);
            if (_hamster.HamsterState.Value == HamsterStateEnum.RoofRun)
                _surfaceController.EnterRoof(_hamster.LastObstacle.Value);
            else
                _surfaceController.EnterRoad();
            _isActive = true;
            EnterRide();
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
        /// Запускает первый или ставит в landing-buffer следующий отдельный прыжок.
        /// </summary>
        public bool TryStartJump()
        {
            if (!_isActive ||
                _hamster.IsDamaged.Value ||
                _jumpsRemaining <= 0)
                return false;

            // Из ride начинаем новую серию без ожидания следующего Update.
            if (_state == SkateboardState.Ride)
            {
                StartJump(isSuper: false, continuesCombo: false);
                return true;
            }

            // После контакта принимаем один следующий jump в combo-buffer.
            if (_state == SkateboardState.Landing && !_isJumpQueued)
            {
                _isJumpQueued = true;
                _isQueuedJumpSuper = false;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Усиливает текущий или поставленный в очередь прыжок без второго списания budget.
        /// </summary>
        public bool TryUpgradeToSuperJump()
        {
            if (!_isActive || _hamster.IsDamaged.Value)
                return false;

            // Upgrade перезапускает authoritative timing усиленного sprite jump.
            if (_state == SkateboardState.Jump)
            {
                _state = SkateboardState.SuperJump;
                _stateTimeLeft = _landingContactTime;
                PlayJump(SkinVisualVariant.Super, _currentJumpActionId);
                return true;
            }

            if (_state == SkateboardState.Landing && _isJumpQueued)
            {
                _isQueuedJumpSuper = true;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Завершает skateboard mode и возвращает normal actor.
        /// </summary>
        public void Complete()
        {
            if (_isDisposed)
                return;

            Deactivate();
        }

        /// <summary>
        /// Обновляет gameplay-time timeout, visual ride-cycle и skateboard FSM.
        /// </summary>
        public void Update()
        {
            if (_isDisposed || !_isActive)
                return;

            // Finish завершает режим сразу; pause оставляет все timers без изменений.
            if (_gameManager.State == GameState.FINISHED)
            {
                Deactivate();
                return;
            }

            bool isPlaying = _gameManager.State == GameState.PLAYING;
            SetVisualPlaybackEnabled(isPlaying);
            if (!isPlaying)
                return;

            // До первого прыжка режим живёт не больше заданного gameplay-time.
            if (_isWaitingForFirstJump)
            {
                _firstJumpTimeLeft -= Time.deltaTime;
                if (_firstJumpTimeLeft <= 0f)
                {
                    Deactivate();
                    return;
                }
            }

            // Каждый state использует собственный authoritative gameplay timer.
            switch (_state)
            {
                case SkateboardState.Ride:
                    UpdateSurface(Time.deltaTime);
                    UpdateRideVisual(Time.deltaTime);
                    break;
                case SkateboardState.Jump:
                case SkateboardState.SuperJump:
                    UpdateJump(Time.deltaTime);
                    break;
                case SkateboardState.Landing:
                    UpdateSurface(Time.deltaTime);
                    UpdateLanding(Time.deltaTime);
                    break;
            }
        }

        /// <summary>
        /// Отписывает runtime и гарантированно возвращает normal actor.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _gameManager.OnFinish -= OnGameFinished;
            _hamster.JumpRequest.Unsubscribe(OnJumpRequested);
            _hamster.RoofJumpRequest.Unsubscribe(OnJumpRequested);
            _hamster.SuperJumpRequest.Unsubscribe(OnSuperJumpRequested);
            _hamster.SuperRoofJumpRequest.Unsubscribe(OnSuperJumpRequested);
            _hamster.DamageEvent.Unsubscribe(OnDamageReceived);
            _landingImpactMechanics.Dispose();
            Deactivate();
            LandingImpact = null;
            _isDisposed = true;
        }

        private void OnGameFinished()
        {
            Deactivate();
        }

        private void OnJumpRequested()
        {
            TryStartJump();
        }

        private void OnSuperJumpRequested()
        {
            TryUpgradeToSuperJump();
        }

        private void OnDamageReceived()
        {
            // Ride использует обычный damage path, затем сразу освобождает mode lease.
            if (_isActive && _state == SkateboardState.Ride)
                Deactivate();
        }

        private bool CanActivateFromCurrentSurface()
        {
            if (_hamster.HamsterState.Value == HamsterStateEnum.Run)
                return true;

            if (_hamster.HamsterState.Value != HamsterStateEnum.RoofRun)
                return false;

            return IsValidRoofSupport(_hamster.LastObstacle.Value);
        }

        private static bool IsValidRoofSupport(Obstacle roof)
        {
            return roof != null &&
                   roof.isActiveAndEnabled &&
                   roof.ObstacleType != null &&
                   CollisionUtils.IsRoofObstacle(
                       roof.ObstacleType.ObstacleTypeEnum);
        }

        private void StartJump(bool isSuper, bool continuesCombo)
        {
            // Списываем budget только при старте отдельного jump-cycle.
            _jumpsRemaining--;
            _comboDepth = continuesCombo ? Mathf.Min(_comboDepth + 1, _jumpBudget) : 1;
            _isJumpQueued = false;
            _isQueuedJumpSuper = false;
            NotifyFirstJumpStarted();

            // Новый cycle получает ActionId; normal-to-super upgrade сохраняет его.
            _currentJumpActionId = ++_nextActionId;
            _state = isSuper ? SkateboardState.SuperJump : SkateboardState.Jump;
            _stateTimeLeft = _landingContactTime;
            PlayJump(
                isSuper ? SkinVisualVariant.Super : SkinVisualVariant.Normal,
                _currentJumpActionId);
        }

        private void UpdateJump(float deltaTime)
        {
            _stateTimeLeft -= deltaTime;
            if (_stateTimeLeft > 0f)
                return;

            // Contact запускает impact один раз; visual landing tail продолжает играть.
            _state = SkateboardState.Landing;
            _stateTimeLeft = _jumpDuration - _landingContactTime;
            _surfaceController.ResolveLandingSupport(
                _hamster.IsOnBottomLine.Value);
            SyncHamsterSurfaceState();
            LandingImpact?.Invoke(_comboDepth);
        }

        private void UpdateLanding(float deltaTime)
        {
            _stateTimeLeft -= deltaTime;
            if (_stateTimeLeft > 0f)
                return;

            // Третий landing завершается только после visual tail.
            if (_jumpsRemaining <= 0)
            {
                Deactivate();
                return;
            }

            if (_isJumpQueued)
            {
                StartJump(_isQueuedJumpSuper, continuesCombo: true);
                return;
            }

            EnterRide();
        }

        private void EnterRide()
        {
            _state = SkateboardState.Ride;
            _stateTimeLeft = 0f;
            _comboDepth = 0;
            _isJumpQueued = false;
            _isQueuedJumpSuper = false;
            PlayNextRideVisual();
        }

        private void UpdateRideVisual(float deltaTime)
        {
            _rideVisualTimeLeft -= deltaTime;
            if (_rideVisualTimeLeft <= 0f)
                PlayNextRideVisual();
        }

        private void PlayNextRideVisual()
        {
            SkinVisualAction action;
            switch (_rideVisualIndex)
            {
                case 0:
                    action = SkinVisualAction.SkateboardRideA;
                    _rideVisualTimeLeft = _rideVisualDuration;
                    break;
                case 1:
                    action = SkinVisualAction.SkateboardRideB;
                    _rideVisualTimeLeft = _rideVisualDuration;
                    break;
                default:
                    action = SkinVisualAction.SkateboardPush;
                    _rideVisualTimeLeft = _pushVisualDuration;
                    break;
            }

            _rideVisualIndex = (_rideVisualIndex + 1) % 3;
            _visualHost.Play(new SkinActionContext(
                action,
                SkinVisualVariant.Normal,
                SkinVisualOutcome.Normal,
                _rideVisualTimeLeft,
                contactTime: null,
                ++_nextActionId));
        }

        private void PlayJump(SkinVisualVariant variant, long actionId)
        {
            _visualHost.Play(new SkinActionContext(
                SkinVisualAction.SkateboardJump,
                variant,
                SkinVisualOutcome.Normal,
                _jumpDuration,
                _landingContactTime,
                actionId));
        }

        private void UpdateSurface(float deltaTime)
        {
            _surfaceController.Tick(
                deltaTime,
                _hamster.IsOnBottomLine.Value);
            SyncHamsterSurfaceState();
        }

        private void SyncHamsterSurfaceState()
        {
            if (_surfaceController.State ==
                SkateboardSurfaceController.SurfaceState.Roof)
            {
                _hamster.LastObstacle.Value = _surfaceController.CurrentRoof;
                _hamster.HamsterState.Value = HamsterStateEnum.RoofRun;
                return;
            }

            _hamster.LastObstacle.Value = null;
            _hamster.HamsterState.Value = HamsterStateEnum.Run;
        }

        private void SetVisualPlaybackEnabled(bool isEnabled)
        {
            if (_isVisualPlaybackEnabled == isEnabled)
                return;

            _isVisualPlaybackEnabled = isEnabled;
            _visualHost.SetPlaybackEnabled(isEnabled);
        }

        private void Deactivate()
        {
            _landingImpactMechanics.Cancel();
            bool shouldRestoreSurface =
                _isActive || _actorSwitcher.IsSkateboardActive;
            Obstacle roof = shouldRestoreSurface &&
                            _surfaceController.State ==
                            SkateboardSurfaceController.SurfaceState.Roof
                ? _surfaceController.CurrentRoof
                : null;
            if (!IsValidRoofSupport(roof))
                roof = null;

            _isActive = false;
            _isWaitingForFirstJump = false;
            _firstJumpTimeLeft = 0f;
            _stateTimeLeft = 0f;
            _rideVisualTimeLeft = 0f;
            _jumpsRemaining = 0;
            _comboDepth = 0;
            _isJumpQueued = false;
            _isQueuedJumpSuper = false;
            _state = SkateboardState.Inactive;
            SetVisualPlaybackEnabled(isEnabled: false);

            if (shouldRestoreSurface)
                _hamster.RestoreNormalSurface(roof);

            _actorSwitcher.ActivateNormal();
            if (shouldRestoreSurface)
                _surfaceController.Reset();
        }

        private enum SkateboardState
        {
            Inactive,
            Ride,
            Jump,
            SuperJump,
            Landing,
        }
    }
}
