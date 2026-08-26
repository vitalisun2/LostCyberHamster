using System;
using Assets.Scripts;
using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;
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
    /// Владеет lifecycle, jump budget, FSM и snapshot каждого Skateboard jump-cycle.
    /// </summary>
    public sealed class SkateboardAttack :
        ISuperAttackRuntime,
        ISkateboardCollisionHandler
    {
        public const float DefaultFirstJumpTimeout = 10f;
        public const float SkateboardPlaybackSpeed = SkateboardVisualSequence.PlaybackSpeed;
        public const float DefaultJumpDuration = 1.25f / SkateboardPlaybackSpeed;
        public const float DefaultLandingContactTime = (10f / 12f) / SkateboardPlaybackSpeed;
        public const int DefaultJumpBudget = 3;
        public const int DefaultChargePerObstacle = 20;

        private readonly Hamster _hamster;
        private readonly HamsterActorSwitcher _actorSwitcher;
        private readonly SkateboardSurfaceController _surfaceController;
        private readonly GameManager _gameManager;
        private readonly SkateboardLandingImpactRuntime _landingImpact;
        private readonly SkateboardVisualSequence _visualSequence;
        private readonly float _firstJumpTimeout;
        private readonly float _jumpDuration;
        private readonly float _landingContactTime;
        private readonly int _jumpBudget;
        private float _firstJumpTimeLeft;
        private float _stateTimeLeft;
        private int _jumpsRemaining;
        private bool _isJumpQueued;
        private bool _isQueuedJumpSuper;
        private bool _isCurrentJumpSuper;
        private bool _isWaitingForFirstJump;
        private bool _isActive;
        private bool _isDisposed;
        private SkateboardJumpCycleSnapshot _currentJumpSnapshot;
        private SkateboardState _state;

        public int ChargePerObstacle { get; }
        public bool IsActive => _isActive;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Сообщает DEV consumers тип landing cycle без влияния на gameplay.
        /// </summary>
        internal event Action<bool> LandingImpact;

        internal bool IsWaitingForFirstJump => _isWaitingForFirstJump;
        internal int JumpsRemaining => _jumpsRemaining;
        internal bool IsRiding => _state == SkateboardState.Ride;
        internal bool IsJumping =>
            _state is SkateboardState.Jump or SkateboardState.SuperJump;
        internal bool IsSuperJumping => _state == SkateboardState.SuperJump;
        internal bool IsLanding => _state == SkateboardState.Landing;
#endif

        /// <summary>
        /// Создаёт mode runtime из явно собранных gameplay, visual, surface и impact частей.
        /// </summary>
        internal SkateboardAttack(
            Hamster hamster,
            HamsterActorSwitcher actorSwitcher,
            SkateboardSurfaceController surfaceController,
            SkateboardVisualSequence visualSequence,
            GameManager gameManager,
            SkateboardLandingImpactRuntime landingImpact,
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
            _visualSequence = visualSequence ??
                throw new ArgumentNullException(nameof(visualSequence));
            _gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
            _landingImpact = landingImpact ??
                throw new ArgumentNullException(nameof(landingImpact));

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
            _visualSequence.Activate();
            EnterRide();
            if (startsOnRoof)
                _surfaceController.AlignToPreparedRoof();
            return true;
        }

        /// <summary>
        /// Запускает первый или буферизует следующий normal jump-cycle.
        /// </summary>
        private bool TryStartJump()
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
                StartJump(isSuper: false);
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
        private bool TryUpgradeToSuperJump()
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
                StartJump(isSuper: true);
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
                _visualSequence.PlayJump(
                    isSuper: true,
                    _currentJumpSnapshot.ActionId);
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
        private bool TryGetJumpSnapshot(out SkateboardJumpCycleSnapshot snapshot)
        {
            bool hasCycle = _isActive &&
                            (_state is SkateboardState.Jump
                                or SkateboardState.SuperJump
                                or SkateboardState.Landing) &&
                            _currentJumpSnapshot.ActionId > 0;
            snapshot = hasCycle ? _currentJumpSnapshot : default;
            return hasCycle;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Возвращает текущий jump snapshot DEV consumers.
        /// </summary>
        internal bool TryGetCurrentJumpSnapshot(out SkateboardJumpCycleSnapshot snapshot)
        {
            return TryGetJumpSnapshot(out snapshot);
        }

        /// <summary>
        /// Завершает mode и возвращает normal actor.
        /// </summary>
        internal void Complete()
        {
            if (!_isDisposed)
                Deactivate();
        }
#endif

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
            _visualSequence.SetPlaybackEnabled(isPlaying);
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
                    _visualSequence.UpdateRide(Time.deltaTime);
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
            _landingImpact.Dispose();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LandingImpact = null;
#endif
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

        SkateboardCollisionResult ISkateboardCollisionHandler.ResolveCollision(
            Obstacle obstacle,
            bool isOnBottomLine)
        {
            if (!_isActive || obstacle == null || obstacle.ObstacleType == null)
            {
                return new SkateboardCollisionResult(
                    SkateboardInteractionPolicy.Outcome.Ignore,
                    wasJumpCollisionActive: false);
            }

            // Gameplay FSM определяет phase и актуальную roof support контакта.
            ObstacleTypeEnum obstacleType = obstacle.ObstacleType.ObstacleTypeEnum;
            bool isRide = _state == SkateboardState.Ride;
            bool hasJumpSnapshot = TryGetJumpSnapshot(
                out SkateboardJumpCycleSnapshot snapshot);
            bool isRideSupport = isRide &&
                                 SkateboardInteractionPolicy.IsRoof(obstacleType) &&
                                 _surfaceController.IsRideSupport(obstacle, isOnBottomLine);
            SkateboardInteractionPolicy.Outcome outcome = isRide
                ? SkateboardInteractionPolicy.DecideRide(obstacleType, isRideSupport)
                : SkateboardInteractionPolicy.DecideJump(
                    obstacleType,
                    hasJumpSnapshot && snapshot.StartedOnRoof);

            // Presentation получает только подтверждённый physical Ride contact.
            if (isRide &&
                !isRideSupport &&
                ObstacleTypePolicy.IsPhysical(obstacleType))
            {
                _visualSequence.ReactToCollision(obstacle);
            }

            return new SkateboardCollisionResult(outcome, hasJumpSnapshot);
        }

        private void StartJump(bool isSuper)
        {
            _jumpsRemaining--;
            _isJumpQueued = false;
            _isQueuedJumpSuper = false;
            _isCurrentJumpSuper = isSuper;
            _isWaitingForFirstJump = false;
            _firstJumpTimeLeft = 0f;

            // Origin берётся один раз до transient surface changes и остаётся immutable.
            long actionId = _visualSequence.BeginJump();
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
            _visualSequence.PlayJump(isSuper, actionId);
        }

        private void UpdateJump(float deltaTime)
        {
            _stateTimeLeft -= deltaTime;
            if (_stateTimeLeft > 0f)
                return;

            _state = SkateboardState.Landing;
            _stateTimeLeft = _jumpDuration - _landingContactTime;

            Obstacle landingSupport = null;
            if (_currentJumpSnapshot.StartedOnRoof)
            {
                landingSupport = _surfaceController.ApplyRoofLandingPlan(
                    _currentJumpSnapshot.LandingPlan);
            }
            else
            {
                _surfaceController.ResolveRoadLanding();
            }
            // Impact запускается в исходной landing-contact точке jump-анимации.
            var impactRequest = new SkateboardLandingImpactRequest(
                _isCurrentJumpSuper,
                _currentJumpSnapshot.StartedOnRoof,
                landingSupport);
            _landingImpact.StartImpact(impactRequest);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LandingImpact?.Invoke(_isCurrentJumpSuper);
#endif
        }

        private SkateboardJumpCycleSnapshot CreateJumpSnapshot(
            long actionId,
            bool startedOnRoof)
        {
            SkateboardLandingSurfacePlan landingPlan = default;
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

            return new SkateboardJumpCycleSnapshot(actionId, startedOnRoof, landingPlan);
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
                StartJump(_isQueuedJumpSuper);
                return;
            }

            EnterRide();
        }

        private void EnterRide()
        {
            _state = SkateboardState.Ride;
            _stateTimeLeft = 0f;
            _isJumpQueued = false;
            _isQueuedJumpSuper = false;
            _isCurrentJumpSuper = false;
            _currentJumpSnapshot = default;
            SyncHamsterSurfaceState();
            _visualSequence.RestartRide();
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

        private void Deactivate(bool cancelLandingImpact = true)
        {
            if (cancelLandingImpact)
                _landingImpact.Cancel();

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
            _jumpsRemaining = 0;
            _isJumpQueued = false;
            _isQueuedJumpSuper = false;
            _isCurrentJumpSuper = false;
            _currentJumpSnapshot = default;
            _state = SkateboardState.Inactive;
            _visualSequence.Deactivate();

            if (shouldRestoreSurface)
                _hamster.RestoreNormalSurface(roof);
            else
                _actorSwitcher.ActivateNormal();
            if (shouldRestoreSurface)
                _surfaceController.ResetSurface();
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
