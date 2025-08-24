using System;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using Assets.Scripts.Installers.Roots;
using Atomic.Elements;
using Atomic.Objects;
using Sirenix.OdinInspector;
using Unity.VisualScripting;
using UnityEngine;
using Vues.GameCore;
using Zenject;

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
        public AtomicVariable<HamsterStateEnum> HamsterState = new(HamsterStateEnum.Run);

        public CollectCoinsOrBonusAction CollectCoinsOrBonusAction;

        public AtomicEvent JumpRequest = new();
        public AtomicEvent SuperJumpRequest = new();
        public AtomicEvent RoofJumpRequest = new();
        public AtomicEvent SuperRoofJumpRequest = new();
        public AtomicEvent JumpOverEvent = new();
        public AtomicEvent DestroyObstacleEvent = new();
        public AtomicEvent DamageEvent = new();
        public AtomicEvent UltaEvent = new();
        public AtomicEvent TapRequest = new();

        public AtomicVariable<Obstacle> LastObstacle = null;

        public AtomicVariable<bool> IsOnBottomLine = new(false);
        public AtomicVariable<int> Lives = new(3);
        public AtomicVariable<int> Energy = new(100);
        public AtomicVariable<bool> IsShifting = new(false);
        public AtomicVariable<bool> IsDamaged = new(false);
        public AtomicVariable<bool> NeedCheckCollisionInRunFromRoofAfterShift = new(false);

        // ulta variables
        public AtomicVariable<bool> IsProtected = new(false);
        public AtomicVariable<bool> IsDestructiveOnCollision = new(false);
        public AtomicVariable<int> UltaChargeAmount = new(0);

        private ShiftTransformAnimatorController _shiftTransformAnimatorController;
        private TransformAnimatorController _transformAnimatorController;
        private SpriteAnimatorController _spriteAnimatorController;
        private SpriteAnimatorEventsDispatcher _spriteAnimatorEventsDispatcher;
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
        private UltaChargeMechanics _ultaChargeMechanics;
        private UltaMechanics _ultaMechanics;
        private DeathMechanics _deathMechanics;
        private TapMechanics _tapMechanics;

        private void Awake()
        {
            _shiftTransformAnimatorController = GetComponentInChildren<ShiftTransformAnimatorController>();
            _transformAnimatorController = GetComponentInChildren<TransformAnimatorController>();
            _spriteAnimatorController = GetComponentInChildren<SpriteAnimatorController>();
            _spriteAnimatorEventsDispatcher = GetComponentInChildren<SpriteAnimatorEventsDispatcher>();
            _transformAnimatorEventsDispatcher = GetComponentInChildren<TransformAnimatorEventsDispatcher>();

            var environmentRoot = GameObject.FindWithTag("EnvironmentRoot").GetComponent<EnvironmentRoot>();

            _tapMechanics = new TapMechanics(
                TapRequest,
                IsOnBottomLine,
                _shiftTransformAnimatorController,
                HamsterState,
                IsShifting,
                IsDamaged);

            CollectCoinsOrBonusAction = new CollectCoinsOrBonusAction(this);

            _jumpMechanics = new JumpMechanics(
                energy: Energy,
                isOnBottomLine: IsOnBottomLine,
                jumpRequest: JumpRequest,
                hamsterState: HamsterState,
                isDamaged: IsDamaged,
                transformAnimatorController: _transformAnimatorController,
                spriteAnimatorController: _spriteAnimatorController,
                characterTransform: transform,
                lastObstacle: LastObstacle,
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
                characterTransform: transform,
                lastObstacle: LastObstacle,
                hamsterWidthInUnits: ColliderWidth);

            _hamsterAnimationEventsMechanics = new HamsterAnimationEventsMechanics(
                transformAnimatorEventsDispatcher: _transformAnimatorEventsDispatcher,
                spriteAnimatorEventsDispatcher: _spriteAnimatorEventsDispatcher,
                hamsterState: HamsterState,
                isDamaged: IsDamaged,
                needCheckCollisionInRunFromRoofAfterShift: NeedCheckCollisionInRunFromRoofAfterShift,
                jumpOverEvent: JumpOverEvent,
                destroyObstacleEvent: DestroyObstacleEvent,
                damageEvent: DamageEvent);

            _roofRunMechanics = new RoofRunMechanics(
                transform: transform,
                lastObstacle: LastObstacle,
                hamsterState: HamsterState,
                isOnBottomLine: IsOnBottomLine,
                transformAnimatorController: _transformAnimatorController,
                hamsterWidthInUnits: ColliderWidth);

            _roofJumpMechanics = new RoofJumpMechanics(
                roofJumpRequest: RoofJumpRequest,
                hamsterState: HamsterState,
                transformAnimatorController: _transformAnimatorController,
                spriteAnimatorController: _spriteAnimatorController,
                transform: transform,
                isOnBottomLine: IsOnBottomLine,
                lastObstacle: LastObstacle,
                energy: Energy,
                hamsterWidthInUnits: ColliderWidth);

            _superRoofJumpMechanics = new SuperRoofJumpMechanics(
                superRoofJumpRequest: SuperRoofJumpRequest,
                hamsterState: HamsterState,
                transformAnimatorController: _transformAnimatorController,
                spriteAnimatorController: _spriteAnimatorController,
                transform: transform,
                isOnBottomLine: IsOnBottomLine,
                lastObstacle: LastObstacle,
                energy: Energy,
                hamsterWidthInUnits: ColliderWidth);

            _takeDamageMechanics = new TakeDamageMechanics(DamageEvent, _spriteAnimatorController, HamsterState, IsDamaged, Lives);
            _addOneCoinMechanics = new AddOneCoinMechanics(JumpOverEvent);
            _addCoinsOrBonusMechanics = new AddCoinsOrBonusMechanics(DestroyObstacleEvent, this);
            _ultaChargeMechanics = new UltaChargeMechanics(DestroyObstacleEvent, UltaChargeAmount);
            _ultaMechanics = new UltaMechanics(UltaEvent, UltaChargeAmount);
            _deathMechanics = new DeathMechanics(Lives);
        }

        private void Update()
        {
            _ultaMechanics.OnUpdate();
            _roofRunMechanics.OnUpdate();
            _tapMechanics.OnUpdate();
        }

        private void OnEnable()
        {
            _jumpMechanics.OnEnable();
            _superJumpMechanics.OnEnable();
            _hamsterAnimationEventsMechanics.OnEnable();
            _takeDamageMechanics.OnEnable();
            _addOneCoinMechanics.OnEnable();
            _addCoinsOrBonusMechanics.OnEnable();
            _ultaChargeMechanics.OnEnable();
            _ultaMechanics.OnEnable();
            _deathMechanics.OnEnable();
            _roofJumpMechanics.OnEnable();
            _superRoofJumpMechanics.OnEnable();
            _tapMechanics.OnEnable();
        }

        private void OnDisable()
        {
            _jumpMechanics.OnDisable();
            _superJumpMechanics.OnDisable();
            _hamsterAnimationEventsMechanics.OnDisable();
            _takeDamageMechanics.OnDisable();
            _addOneCoinMechanics.OnDisable();
            _addCoinsOrBonusMechanics.OnDisable();
            _ultaChargeMechanics.OnDisable();
            _ultaMechanics.OnDisable();
            _deathMechanics.OnDisable();
            _roofJumpMechanics.OnDisable();
            _superRoofJumpMechanics.OnDisable();
            _tapMechanics.OnDisable();
        }

        public void AddEnergy(int value = 20)
        {
            var energyToAdd = Mathf.Min(100 - Energy.Value, value);
            Energy.Value += energyToAdd;
            if (energyToAdd > 0)
            {
                GameEventsManager.EnergyAdded(energyToAdd);
            }
        }

        public void AddUltaCharge()
        {
            var value = SkinManager.CurrentSkin.UltaCharge;
            AddUltaCharge(value);
        }

        public void AddUltaCharge(int value)
        {
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
            if (boxCollider2D == null)
                throw new MissingComponentException("BoxCollider2D is missing on Hamster object.");

            return boxCollider2D.size.x;
        }

        private float GetBoxColliderHeight()
        {
            var boxCollider2D = transform.GetComponentInChildren<BoxCollider2D>();
            if (boxCollider2D == null)
                throw new MissingComponentException("BoxCollider2D is missing on Hamster object.");

            return boxCollider2D.size.y;
        }
    }
}
