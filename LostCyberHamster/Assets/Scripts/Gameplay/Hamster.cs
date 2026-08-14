using System;
using Assets.Scripts.Common.Models;
using Assets.Scripts.GameEngine.Actors;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Skins;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using Assets.Scripts.Installers.Roots;
using Atomic.Elements;
using Atomic.Objects;
using Sirenix.OdinInspector;
using UnityEngine;
using Vues.GameCore;

namespace Assets.Scripts.Gameplay
{
    public class Hamster : AtomicObject
    {
        /// <summary>
        /// Independent transform for effects
        /// </summary>
        [SerializeField]
        [InfoBox("Independent transform for effects")]
        private Transform _effectsSlot;

        [HideInInspector]
        public Transform EffectsSlot => _effectsSlot;
        public float ColliderWidth => GetBoxColliderWidth();
        public float ColliderHeight => GetBoxColliderHeight();
        public float LeftX { get; private set; }
        public float RightX { get; private set; }
        public int RunScore => _runScoreMechanics?.CurrentScore ?? 0;
        public RunResultData LatestRunResult =>
            _partOfDayScoreMechanics?.LatestResult;

        public event Action<RunResultData> RunResultChanged
        {
            add
            {
                if (_partOfDayScoreMechanics != null)
                    _partOfDayScoreMechanics.ResultChanged += value;
            }
            remove
            {
                if (_partOfDayScoreMechanics != null)
                    _partOfDayScoreMechanics.ResultChanged -= value;
            }
        }

        public AtomicVariable<HamsterStateEnum> HamsterState = new(HamsterStateEnum.Run);

        public CollectCoinsOrBonusAction CollectCoinsOrBonusAction;

        public AtomicEvent JumpRequest = new();
        public AtomicEvent SuperJumpRequest = new();
        public AtomicEvent RoofJumpRequest = new();
        public AtomicEvent SuperRoofJumpRequest = new();
        public AtomicEvent JumpOverEvent = new();
        public AtomicEvent<ObstacleTypeEnum> CollectableCollectedEvent = new();
        public AtomicEvent<Obstacle> DestroyObstacleEvent = new();
        public AtomicEvent<Obstacle> DestroyObstacleBySuperAttackEvent = new();
        public AtomicEvent DamageEvent = new();
        public AtomicEvent UltaEvent = new();
        public AtomicEvent TapRequest = new();

        public AtomicVariable<Obstacle> LastObstacle = null;
        public AtomicVariable<Obstacle> PendingJumpedOnObstacle = null;

        public AtomicVariable<bool> IsOnBottomLine = new(false);
        public AtomicVariable<int> Lives = new(3);
        public AtomicVariable<int> Energy = new(100);
        public AtomicVariable<bool> IsShifting = new(false);
        public AtomicVariable<bool> IsDamaged = new(false);
        public AtomicVariable<bool> NeedCheckCollisionInRunFromRoofAfterShift = new(false);

        // ulta variables
        public AtomicVariable<bool> IsProtected = new(false);
        public AtomicVariable<bool> IsSuperAttackDestructiveOnCollision = new(false);
        public AtomicVariable<int> UltaChargeAmount = new(0);

        private ShiftTransformAnimatorController _shiftTransformAnimatorController;
        private TransformAnimatorController _transformAnimatorController;
        private SpriteAnimatorController _spriteAnimatorController;
        [SerializeField] private HamsterActorSwitcher _actorSwitcher;
        [SerializeField] private SkateboardSurfaceController _skateboardSurfaceController;
        [SerializeField] private SkinVisualHost _normalSkinVisualHost;
        [SerializeField] private SkinVisualHost _skateboardSkinVisualHost;
        private TransformAnimatorEventsDispatcher _transformAnimatorEventsDispatcher;

        private JumpMechanics _jumpMechanics;
        private SuperJumpMechanics _superJumpMechanics;
        private RoofRunMechanics _roofRunMechanics;
        private RoofJumpMechanics _roofJumpMechanics;
        private SuperRoofJumpMechanics _superRoofJumpMechanics;
        private HamsterAnimationEventsMechanics _hamsterAnimationEventsMechanics;
        private TakeDamageMechanics _takeDamageMechanics;
        private AddOneCoinMechanics _addOneCoinMechanics;
        private AddCoinsOrBonusMechanics _addCoinsOrBonusMechanics;
        private RunScoreMechanics _runScoreMechanics;
        private PartOfDayScoreMechanics _partOfDayScoreMechanics;
        private DeathMechanics _deathMechanics;
        private TapMechanics _tapMechanics;
        private ISuperAttackRuntime _superAttackRuntime;
        private SkinVisualRuntime _normalSkinVisualRuntime;
        private SkinVisualRuntime _skateboardSkinVisualRuntime;
        private UltaMechanics _ultaMechanics;
        private UltaChargeMechanics _ultaChargeMechanics;

        public bool HasSuperAttack => _superAttackRuntime != null;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Возвращает настроенный skateboard runtime только DEV testing tools.
        /// </summary>
        public SkateboardAttack SkateboardAttackRuntimeForTesting =>
            _superAttackRuntime as SkateboardAttack;
#endif

        private void Awake()
        {
            // Normal actor должен быть включён до кеширования обычных mechanics-компонентов.
            if (_actorSwitcher == null)
                throw new MissingReferenceException("HamsterActorSwitcher reference is missing.");
            _actorSwitcher.Initialize();

            CacheHorizontalBounds();

            _shiftTransformAnimatorController = GetComponentInChildren<ShiftTransformAnimatorController>();
            _transformAnimatorController = GetComponentInChildren<TransformAnimatorController>();
            _spriteAnimatorController = GetComponentInChildren<SpriteAnimatorController>();
            _transformAnimatorEventsDispatcher = GetComponentInChildren<TransformAnimatorEventsDispatcher>();

            var environmentRoot = GameObject.FindWithTag("EnvironmentRoot").GetComponent<EnvironmentRoot>();

            _tapMechanics = new TapMechanics(
                TapRequest,
                IsOnBottomLine,
                _shiftTransformAnimatorController,
                HamsterState,
                IsShifting);

            CollectCoinsOrBonusAction = new CollectCoinsOrBonusAction(this);

            _jumpMechanics = new JumpMechanics(
                energy: Energy,
                isOnBottomLine: IsOnBottomLine,
                jumpRequest: JumpRequest,
                hamsterState: HamsterState,
                isDamaged: IsDamaged,
                transformAnimatorController: _transformAnimatorController,
                spriteAnimatorController: _spriteAnimatorController,
                actorSwitcher: _actorSwitcher,
                characterTransform: transform,
                lastObstacle: LastObstacle,
                pendingJumpedOnObstacle: PendingJumpedOnObstacle,
                hamsterWidthInUnits: ColliderWidth,
                hamsterHeightInUnits: ColliderHeight);

            _superJumpMechanics = new SuperJumpMechanics(
                superJumpRequest: SuperJumpRequest,
                energy: Energy,
                isOnBottomLine: IsOnBottomLine,
                hamsterState: HamsterState,
                isDamaged: IsDamaged,
                transformAnimatorController: _transformAnimatorController,
                spriteAnimatorController: _spriteAnimatorController,
                actorSwitcher: _actorSwitcher,
                characterTransform: transform,
                lastObstacle: LastObstacle,
                pendingJumpedOnObstacle: PendingJumpedOnObstacle,
                hamsterWidthInUnits: ColliderWidth);

            _hamsterAnimationEventsMechanics = new HamsterAnimationEventsMechanics(
                transformAnimatorEventsDispatcher: _transformAnimatorEventsDispatcher,
                spriteAnimatorController: _spriteAnimatorController,
                hamsterState: HamsterState,
                needCheckCollisionInRunFromRoofAfterShift: NeedCheckCollisionInRunFromRoofAfterShift,
                jumpOverEvent: JumpOverEvent,
                destroyObstacleEvent: DestroyObstacleEvent,
                pendingJumpedOnObstacle: PendingJumpedOnObstacle,
                damageEvent: DamageEvent);

            _roofRunMechanics = new RoofRunMechanics(
                transform: transform,
                lastObstacle: LastObstacle,
                hamsterState: HamsterState,
                isOnBottomLine: IsOnBottomLine,
                isDamaged: IsDamaged,
                transformAnimatorController: _transformAnimatorController,
                spriteAnimatorController: _spriteAnimatorController,
                hamsterWidthInUnits: ColliderWidth);

            _roofJumpMechanics = new RoofJumpMechanics(
                roofJumpRequest: RoofJumpRequest,
                hamsterState: HamsterState,
                transformAnimatorController: _transformAnimatorController,
                spriteAnimatorController: _spriteAnimatorController,
                actorSwitcher: _actorSwitcher,
                transform: transform,
                isOnBottomLine: IsOnBottomLine,
                lastObstacle: LastObstacle,
                pendingJumpedOnObstacle: PendingJumpedOnObstacle,
                energy: Energy,
                hamsterWidthInUnits: ColliderWidth);

            _superRoofJumpMechanics = new SuperRoofJumpMechanics(
                superRoofJumpRequest: SuperRoofJumpRequest,
                hamsterState: HamsterState,
                transformAnimatorController: _transformAnimatorController,
                spriteAnimatorController: _spriteAnimatorController,
                actorSwitcher: _actorSwitcher,
                transform: transform,
                isOnBottomLine: IsOnBottomLine,
                lastObstacle: LastObstacle,
                pendingJumpedOnObstacle: PendingJumpedOnObstacle,
                energy: Energy,
                hamsterWidthInUnits: ColliderWidth);

            _takeDamageMechanics = new TakeDamageMechanics(
                DamageEvent,
                _spriteAnimatorController,
                IsDamaged,
                Lives,
                LevelController.Instance.LevelData.GameManager);
            _addOneCoinMechanics = new AddOneCoinMechanics(JumpOverEvent);
            _addCoinsOrBonusMechanics = new AddCoinsOrBonusMechanics(DestroyObstacleEvent, this);
            _runScoreMechanics = new RunScoreMechanics(
                CollectableCollectedEvent,
                DestroyObstacleEvent,
                DestroyObstacleBySuperAttackEvent,
                Lives,
                LevelController.Instance.LevelData.GameManager);
            _partOfDayScoreMechanics = new PartOfDayScoreMechanics(
                _runScoreMechanics,
                Lives,
                LevelController.Instance.LevelData.GameManager);
            _deathMechanics = new DeathMechanics(Lives);
        }

        private void Update()
        {
            if (!_actorSwitcher.IsSkateboardActive)
                _roofRunMechanics.OnUpdate();
            _tapMechanics.OnUpdate();
            _takeDamageMechanics.OnUpdate(Time.deltaTime);
            _ultaMechanics?.OnUpdate();
        }

        private void OnEnable()
        {
            _jumpMechanics.OnEnable();
            _superJumpMechanics.OnEnable();
            _hamsterAnimationEventsMechanics.OnEnable();
            _takeDamageMechanics.OnEnable();
            _runScoreMechanics.OnEnable();
            _partOfDayScoreMechanics.OnEnable();
            _addOneCoinMechanics.OnEnable();
            _addCoinsOrBonusMechanics.OnEnable();
            _deathMechanics.OnEnable();
            _roofJumpMechanics.OnEnable();
            _superRoofJumpMechanics.OnEnable();
            _tapMechanics.OnEnable();
            _ultaChargeMechanics?.OnEnable();
            _ultaMechanics?.OnEnable();
        }

        private void OnDisable()
        {
            _jumpMechanics.OnDisable();
            _superJumpMechanics.OnDisable();
            _hamsterAnimationEventsMechanics.OnDisable();
            _takeDamageMechanics.OnDisable();
            _runScoreMechanics.OnDisable();
            _partOfDayScoreMechanics.OnDisable();
            _addOneCoinMechanics.OnDisable();
            _addCoinsOrBonusMechanics.OnDisable();
            _deathMechanics.OnDisable();
            _roofJumpMechanics.OnDisable();
            _superRoofJumpMechanics.OnDisable();
            _tapMechanics.OnDisable();
            _ultaChargeMechanics?.OnDisable();
            _ultaMechanics?.OnDisable();
        }

        private void OnDestroy()
        {
            _normalSkinVisualRuntime?.Dispose();
            _skateboardSkinVisualRuntime?.Dispose();
            _superAttackRuntime?.Dispose();
        }

        public HamsterActorSwitcher ActorSwitcher => _actorSwitcher;
        public SkateboardSurfaceController SkateboardSurfaceController =>
            _skateboardSurfaceController;
        public SkinVisualHost SkinVisualHost => _normalSkinVisualHost;
        public SkinVisualHost NormalSkinVisualHost => _normalSkinVisualHost;
        public SkinVisualHost SkateboardSkinVisualHost => _skateboardSkinVisualHost;

        /// <summary>
        /// Возвращает признак активного skateboard gameplay mode.
        /// </summary>
        public bool IsSkateboardModeActive =>
            _superAttackRuntime is SkateboardAttack skateboardAttack &&
            skateboardAttack.IsActive;

        /// <summary>
        /// Возвращает признак разрушительного collision-состояния skateboard jump.
        /// </summary>
        public bool IsSkateboardJumpCollisionActive =>
            _superAttackRuntime is SkateboardAttack skateboardAttack &&
            skateboardAttack.IsActive &&
            (skateboardAttack.IsJumping || skateboardAttack.IsLanding);

        /// <summary>
        /// Подготавливает normal actor к возврату на дорогу или текущую roof support.
        /// </summary>
        public void RestoreNormalSurface(Obstacle roof)
        {
            if (roof == null)
            {
                // Road exit очищает stale roof state и возвращает transform Animator в default pose.
                LastObstacle.Value = null;
                HamsterState.Value = HamsterStateEnum.Run;
                _transformAnimatorController.ResetToRunSurface();
                _spriteAnimatorController.PlayForState(HamsterStateEnum.Run);
                return;
            }

            // Roof exit сохраняет текущую опору и выбирает clips её высоты.
            LastObstacle.Value = roof;
            HamsterState.Value = HamsterStateEnum.RoofRun;
            bool isMediumRoof =
                roof.ObstacleType.ObstacleTypeEnum == ObstacleTypeEnum.mediumNotAlive;
            _transformAnimatorController.SwapRoofClips(isMediumRoof);
            _spriteAnimatorController.PlayForState(HamsterStateEnum.RoofRun);
        }

        /// <summary>
        /// Передаёт Hamster владение visual runtimes обоих actor modes.
        /// </summary>
        public void ConfigureSkinVisuals(
            SkinVisualRuntime normal,
            SkinVisualRuntime skateboard)
        {
            // Оба mode visuals обязательны до передачи ownership.
            if (normal == null)
                throw new ArgumentNullException(nameof(normal));
            if (skateboard == null)
                throw new ArgumentNullException(nameof(skateboard));

            // Заменяем прежние leases только после полной проверки аргументов.
            _normalSkinVisualRuntime?.Dispose();
            _skateboardSkinVisualRuntime?.Dispose();
            _normalSkinVisualRuntime = normal;
            _skateboardSkinVisualRuntime = skateboard;
        }

        public void ConfigureSuperAttack(ISuperAttackRuntime runtime)
        {
            // Подключаем прежние механику заряда и механику применения.
            _superAttackRuntime = runtime;
            if (_superAttackRuntime == null)
            {
                return;
            }

            _ultaMechanics = new UltaMechanics(
                UltaEvent,
                UltaChargeAmount,
                () => runtime.TryActivate(),
                runtime.Update);
            _ultaChargeMechanics = new UltaChargeMechanics(
                DestroyObstacleEvent,
                UltaChargeAmount,
                runtime.ChargePerObstacle);

            // Настройка активного персонажа включает подписки сразу.
            if (isActiveAndEnabled)
            {
                _ultaChargeMechanics.OnEnable();
                _ultaMechanics.OnEnable();
            }
        }

        public void AddEnergy(int value = 30)
        {
            var energyToAdd = Mathf.Min(100 - Energy.Value, value);
            Energy.Value += energyToAdd;
            if (energyToAdd > 0)
            {
                GameEventsManager.EnergyAdded(energyToAdd);
            }
        }

        public void AddUltaCharge(int value)
        {
            if (!HasSuperAttack)
            {
                return;
            }

            var ultaToAdd = Mathf.Min(100 - UltaChargeAmount.Value, value);
            UltaChargeAmount.Value += ultaToAdd;

            if (UltaChargeAmount.Value == 100 && ultaToAdd > 0)
            {
                GameEventsManager.UltaActivated();
            }
        }

        private float GetBoxColliderWidth()
        {
            var boxCollider2D = transform.GetComponentInChildren<BoxCollider2D>();
            if (boxCollider2D == null) throw new MissingComponentException("BoxCollider2D is missing on Hamster object.");

            return boxCollider2D.size.x;
        }

        private float GetBoxColliderHeight()
        {
            var boxCollider2D = transform.GetComponentInChildren<BoxCollider2D>();
            if (boxCollider2D == null) throw new MissingComponentException("BoxCollider2D is missing on Hamster object.");

            return boxCollider2D.bounds.size.y;
        }

        private void CacheHorizontalBounds()
        {
            var box = GetComponentInChildren<BoxCollider2D>();
            var b = box.bounds;          // уже в мировых координатах
            LeftX = b.min.x;
            RightX = b.max.x;
        }
    }
}
