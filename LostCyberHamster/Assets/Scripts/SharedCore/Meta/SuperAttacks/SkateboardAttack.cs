using System;
using Assets.Scripts;
using Assets.Scripts.GameEngine.Actors;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Skins;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using UnityEngine;

namespace Vues.GameCore
{
    /// <summary>
    /// Владеет lifecycle, jump budget, combo, FSM и snapshot каждого Skateboard jump-cycle.
    /// </summary>
    public sealed class SkateboardAttack : ISuperAttackRuntime
    {
        public const float DefaultFirstJumpTimeout = 10f;
        public const float SkateboardPlaybackSpeed = 1.5f;
        public const float DefaultJumpDuration = 1.25f / SkateboardPlaybackSpeed;
        public const float DefaultLandingContactTime = (10f / 12f) / SkateboardPlaybackSpeed;
        public const int DefaultJumpBudget = 3;
        public const int DefaultChargePerObstacle = 20;

        private const float _rideVisualDuration = (8f / 12f) / SkateboardPlaybackSpeed;
        private const float _pushVisualDuration = (11f / 12f) / SkateboardPlaybackSpeed;

        /// <summary>
        /// Неизменяемый origin и landing intent одного jump-cycle.
        /// </summary>
        public readonly struct JumpCycleSnapshot
        {
            public JumpCycleSnapshot(
                long actionId,
                bool startedOnRoof,
                SkateboardSurfaceController.LandingSurfacePlan landingPlan)
            {
                ActionId = actionId;
                StartedOnRoof = startedOnRoof;
                LandingPlan = landingPlan;
            }

            public long ActionId { get; }
            public bool StartedOnRoof { get; }
            public SkateboardSurfaceController.LandingSurfacePlan LandingPlan { get; }
        }

        private readonly Hamster _hamster;
        private readonly HamsterActorSwitcher _actorSwitcher;
        private readonly SkateboardSurfaceController _surfaceController;
        private readonly SkinVisualHost _visualHost;
        private readonly GameManager _gameManager;
        private readonly SkateboardLandingImpactMechanics _landingImpactMechanics;
        private readonly float _firstJumpTimeout;
        private readonly float _jumpDuration;
        private readonly float _landingContactTime;
        private readonly int _jumpBudget;

        private float _firstJumpTimeLeft;
        private float _stateTimeLeft;
        private float _rideVisualTimeLeft;
        private long _nextActionId;
        private int _rideVisualIndex;
        private int _jumpsRemaining;
        private int _comboDepth;
        private bool _isJumpQueued;
        private bool _isQueuedJumpSuper;
        private bool _isCurrentJumpSuper;
        private bool _isWaitingForFirstJump;
        private bool _isVisualPlaybackEnabled;
        private bool _isActive;
        private bool _isDisposed;
        private JumpCycleSnapshot _currentJumpSnapshot;
        private SkateboardState _state;

        /// <summary>
        /// Сообщает DEV consumers уровень combo и тип cycle в landing contact frame.
        /// </summary>
        public event Action<int, bool> LandingImpact;

        public int ChargePerObstacle { get; }
        public bool IsActive => _isActive;
        public bool IsWaitingForFirstJump => _isWaitingForFirstJump;
        public int JumpsRemaining => _jumpsRemaining;
        public int ComboDepth => _comboDepth;
        public bool IsRiding => _state == SkateboardState.Ride;
        public bool IsJumping =>
            _state is SkateboardState.Jump or SkateboardState.SuperJump;
        public bool IsSuperJumping => _state == SkateboardState.SuperJump;
        public bool IsLanding => _state == SkateboardState.Landing;

        /// <summary>
        /// Создаёт mode runtime из явно собранных gameplay, visual, surface и impact частей.
        /// </summary>
        public SkateboardAttack(
            Hamster hamster,
            HamsterActorSwitcher actorSwitcher,
            SkateboardSurfaceController surfaceController,
            SkinVisualHost visualHost,
            GameManager gameManager,
            SkateboardLandingImpactMechanics landingImpactMechanics,
            float firstJumpTimeout = DefaultFirstJumpTimeout,
            int chargePerObstacle = DefaultChargePerObstacle,
            float jumpDuration = DefaultJumpDuration,
            float landingContactTime = DefaultLandingContactTime,
            int jumpBudget = DefaultJumpBudget)
        {
            _hamster = hamster ?? throw new ArgumentNullException(nameof(hamster));
            _actorSwitcher = actorSwitcher ?? throw new ArgumentNullException(nameof(actorSwitcher));
            _surfaceController = surfaceController ??
                throw new ArgumentNullException(nameof(surfaceController));
            _visualHost = visualHost ?? throw new ArgumentNullException(nameof(visualHost));
            _gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
            _landingImpactMechanics = landingImpactMechanics ??
                throw new ArgumentNullException(nameof(landingImpactMechanics));

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

            _gameManager.OnFinish += OnGameFinished;
            _hamster.JumpRequest.Subscribe(OnJumpRequested);
            _hamster.RoofJumpRequest.Subscribe(OnJumpRequested);
            _hamster.SuperJumpRequest.Subscribe(OnSuperJumpRequested);
            _hamster.SuperRoofJumpRequest.Subscribe(OnSuperJumpRequested);
            _hamster.DamageEvent.Subscribe(OnDamageReceived);
        }

        /// <summary>
        /// Включает mode со стабильной road или roof surface только во время gameplay.
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

            // Source surface фиксируется до actor switch и первого physics callback.
            bool startsOnRoof = _hamster.HamsterState.Value == HamsterStateEnum.RoofRun;
            Obstacle initialRoof = startsOnRoof ? _hamster.LastObstacle.Value : null;
            _firstJumpTimeLeft = _firstJumpTimeout;
            _isWaitingForFirstJump = true;
            _jumpsRemaining = _jumpBudget;
            _comboDepth = 0;
            _rideVisualIndex = 0;
            _isJumpQueued = false;
            _isQueuedJumpSuper = false;
            _isCurrentJumpSuper = false;
            _currentJumpSnapshot = default;

            if (startsOnRoof)
                _surfaceController.PrepareRoof(initialRoof);
            else
                _surfaceController.EnterRoad();

            // Gameplay authority включается раньше colliders; callbacks уже видят mode policy.
            _isActive = true;
            _state = SkateboardState.Ride;
            _actorSwitcher.ActivateSkateboard();
            _visualHost.Rebind();
            SetVisualPlaybackEnabled(isEnabled: true);
            EnterRide();
            if (startsOnRoof)
                _surfaceController.AlignToPreparedRoof();
            return true;
        }

        /// <summary>
        /// Запускает первый или буферизует следующий normal jump-cycle.
        /// </summary>
        public bool TryStartJump()
        {
            if (!_isActive ||
                _gameManager.State != GameState.PLAYING ||
                _hamster.IsDamaged.Value ||
                _jumpsRemaining <= 0)
            {
                return false;
            }

            if (_state == SkateboardState.Ride)
            {
                StartJump(isSuper: false, continuesCombo: false);
                return true;
            }

            if (_state == SkateboardState.Landing && !_isJumpQueued)
            {
                _isJumpQueued = true;
                _isQueuedJumpSuper = false;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Стартует super cycle из Ride либо усиливает текущий/queued cycle без второго budget.
        /// </summary>
        public bool TryUpgradeToSuperJump()
        {
            if (!_isActive ||
                _gameManager.State != GameState.PLAYING ||
                _hamster.IsDamaged.Value)
            {
                return false;
            }

            // Первый double-tap после actor switch не теряется из-за stale external detector.
            if (_state == SkateboardState.Ride && _jumpsRemaining > 0)
            {
                StartJump(isSuper: true, continuesCombo: false);
                return true;
            }

            if (_state == SkateboardState.Jump)
            {
                _isCurrentJumpSuper = true;
                _state = SkateboardState.SuperJump;
                _stateTimeLeft = _landingContactTime;
                // Contact timer перезапущен: новый intent считается из текущих world positions.
                _currentJumpSnapshot = CreateJumpSnapshot(
                    _currentJumpSnapshot.ActionId,
                    _currentJumpSnapshot.StartedOnRoof);
                SetHamsterJumpState(isSuper: true);
                PlayJump(SkinVisualVariant.Super, _currentJumpSnapshot.ActionId);
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
        /// Возвращает immutable snapshot только для active Jump/SuperJump/Landing cycle.
        /// </summary>
        public bool TryGetCurrentJumpSnapshot(out JumpCycleSnapshot snapshot)
        {
            bool hasCycle = _isActive &&
                            (_state is SkateboardState.Jump
                                or SkateboardState.SuperJump
                                or SkateboardState.Landing) &&
                            _currentJumpSnapshot.ActionId > 0;
            snapshot = hasCycle ? _currentJumpSnapshot : default;
            return hasCycle;
        }

        /// <summary>
        /// Завершает mode и возвращает normal actor.
        /// </summary>
        public void Complete()
        {
            if (!_isDisposed)
                Deactivate();
        }

        /// <summary>
        /// Обновляет gameplay-time timeout, visual cycle и FSM.
        /// </summary>
        public void Update()
        {
            if (_isDisposed || !_isActive)
                return;

            if (_gameManager.State == GameState.FINISHED)
            {
                Deactivate();
                return;
            }

            bool isPlaying = _gameManager.State == GameState.PLAYING;
            SetVisualPlaybackEnabled(isPlaying);
            if (!isPlaying)
                return;

            if (_isWaitingForFirstJump)
            {
                _firstJumpTimeLeft -= Time.deltaTime;
                if (_firstJumpTimeLeft <= 0f)
                {
                    Deactivate();
                    return;
                }
            }

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
                    UpdateSurface(Time.deltaTime, syncHamsterState: false);
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
            Deactivate();
            _landingImpactMechanics.Dispose();
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
            if (_isActive && _state == SkateboardState.Ride)
                Deactivate();
        }

        private bool CanActivateFromCurrentSurface()
        {
            if (_hamster.HamsterState.Value == HamsterStateEnum.Run)
                return true;

            return _hamster.HamsterState.Value == HamsterStateEnum.RoofRun &&
                   IsValidRoofSupport(_hamster.LastObstacle.Value);
        }

        private static bool IsValidRoofSupport(Obstacle roof)
        {
            return roof != null &&
                   roof.isActiveAndEnabled &&
                   roof.ObstacleType != null &&
                   SkateboardInteractionPolicy.IsRoof(
                       roof.ObstacleType.ObstacleTypeEnum);
        }

        private void StartJump(bool isSuper, bool continuesCombo)
        {
            _jumpsRemaining--;
            _comboDepth = continuesCombo
                ? Mathf.Min(_comboDepth + 1, _jumpBudget)
                : 1;
            _isJumpQueued = false;
            _isQueuedJumpSuper = false;
            _isCurrentJumpSuper = isSuper;
            _isWaitingForFirstJump = false;
            _firstJumpTimeLeft = 0f;

            // Origin берётся один раз до transient surface changes и остаётся immutable.
            long actionId = ++_nextActionId;
            bool startedOnRoof =
                _surfaceController.State == SkateboardSurfaceController.SurfaceState.Roof &&
                IsValidRoofSupport(_surfaceController.CurrentRoof);
            _currentJumpSnapshot = CreateJumpSnapshot(actionId, startedOnRoof);
            if (!startedOnRoof &&
                _surfaceController.State != SkateboardSurfaceController.SurfaceState.Road)
            {
                _surfaceController.EnterRoad();
            }

            _state = isSuper ? SkateboardState.SuperJump : SkateboardState.Jump;
            _stateTimeLeft = _landingContactTime;
            SetHamsterJumpState(isSuper);
            PlayJump(
                isSuper ? SkinVisualVariant.Super : SkinVisualVariant.Normal,
                actionId);
        }

        private void UpdateJump(float deltaTime)
        {
            _stateTimeLeft -= deltaTime;
            if (_stateTimeLeft > 0f)
                return;

            _state = SkateboardState.Landing;
            _stateTimeLeft = _jumpDuration - _landingContactTime;

            SkateboardSurfaceController.LandingSurfaceResult surfaceResult = default;
            if (_currentJumpSnapshot.StartedOnRoof)
            {
                surfaceResult = _surfaceController.ApplyRoofLandingPlan(
                    _currentJumpSnapshot.LandingPlan);
            }
            else
            {
                _surfaceController.ResolveRoadLanding();
            }
            // Miss и wave используют тот же immutable cycle origin; поздний surface state не читается.
            var impactRequest = new SkateboardLandingImpactMechanics.ImpactRequest(
                _currentJumpSnapshot.ActionId,
                _comboDepth,
                _isCurrentJumpSuper,
                _currentJumpSnapshot.StartedOnRoof,
                surfaceResult.Support,
                surfaceResult.MissedRoof);
            _landingImpactMechanics.StartImpact(impactRequest);
            LandingImpact?.Invoke(_comboDepth, _isCurrentJumpSuper);
        }

        private JumpCycleSnapshot CreateJumpSnapshot(long actionId, bool startedOnRoof)
        {
            SkateboardSurfaceController.LandingSurfacePlan landingPlan = default;
            if (startedOnRoof)
            {
                float worldTravel =
                    _landingContactTime *
                    Consts.RoadScrollSpeed *
                    ScrollLeftMechanics.SpeedMultiplier;
                landingPlan = _surfaceController.PredictRoofLanding(
                    _hamster.IsOnBottomLine.Value,
                    worldTravel);
            }

            return new JumpCycleSnapshot(actionId, startedOnRoof, landingPlan);
        }

        private void UpdateLanding(float deltaTime)
        {
            _stateTimeLeft -= deltaTime;
            if (_stateTimeLeft > 0f)
                return;

            if (_jumpsRemaining <= 0)
            {
                // Natural exit не обрывает дальнюю wave, живущую отдельным listener.
                Deactivate(cancelLandingImpact: false);
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
            _isCurrentJumpSuper = false;
            _currentJumpSnapshot = default;
            SyncHamsterSurfaceState();
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
                _rideVisualTimeLeft * SkateboardPlaybackSpeed,
                ++_nextActionId,
                SkateboardPlaybackSpeed));
        }

        private void PlayJump(SkinVisualVariant variant, long actionId)
        {
            _visualHost.Play(new SkinActionContext(
                SkinVisualAction.SkateboardJump,
                variant,
                SkinVisualOutcome.Normal,
                _jumpDuration * SkateboardPlaybackSpeed,
                actionId,
                SkateboardPlaybackSpeed));
        }

        private void UpdateSurface(float deltaTime, bool syncHamsterState = true)
        {
            // Shared lane shift движется независимо; roof ownership меняется после shift.
            if (_hamster.IsShifting.Value &&
                _surfaceController.State == SkateboardSurfaceController.SurfaceState.Roof)
            {
                return;
            }

            _surfaceController.Tick(deltaTime, _hamster.IsOnBottomLine.Value);
            if (syncHamsterState)
                SyncHamsterSurfaceState();
        }

        private void SetHamsterJumpState(bool isSuper)
        {
            _hamster.HamsterState.Value = _currentJumpSnapshot.StartedOnRoof
                ? isSuper
                    ? HamsterStateEnum.SuperRoofJump
                    : HamsterStateEnum.RoofJump
                : isSuper
                    ? HamsterStateEnum.SuperJump
                    : HamsterStateEnum.Jump;
        }

        private void SyncHamsterSurfaceState()
        {
            if (_surfaceController.State == SkateboardSurfaceController.SurfaceState.Roof)
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

        private void Deactivate(bool cancelLandingImpact = true)
        {
            if (cancelLandingImpact)
                _landingImpactMechanics.Cancel();

            bool shouldRestoreSurface = _isActive || _actorSwitcher.IsSkateboardActive;
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
            _isCurrentJumpSuper = false;
            _currentJumpSnapshot = default;
            _state = SkateboardState.Inactive;
            SetVisualPlaybackEnabled(isEnabled: false);

            if (shouldRestoreSurface)
                _hamster.RestoreNormalSurface(roof);
            else
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
            Landing
        }
    }
}
