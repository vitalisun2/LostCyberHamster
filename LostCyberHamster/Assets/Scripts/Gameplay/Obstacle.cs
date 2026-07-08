using Assets.Scripts.Common.Models;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.System;
using Atomic.Elements;
using Atomic.Objects;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Assets.Scripts.Gameplay
{
    public class Obstacle : AtomicObject,
        Listeners.IGameUpdateListener,
        Listeners.IGamePauseListener,
        Listeners.IGameResumeListener
    {
        public Hamster Hamster { get; private set; }

        public ObstacleType ObstacleType { get; private set; }

        public float ColliderWidth => GetBoxColliderWidth();
        public float ColliderHeight => GetBoxColliderHeight();

        // obstacle identifier, used for debugging
        [ShowInInspector]
        public string ObstacleId { get; private set; }

        public BoomEffectAction BoomEffectAction { get; private set; }
        public GameManager GameManager { get; private set; }
        
        public AnimationType AnimationType { get; private set; }


        public AtomicEvent<GameObject> OnObstacleUnspawned = new();

        private AtomicVariable<BoomEffect> _boomEffect;

        private ScrollLeftMechanics _scrollLeftMechanics;
        private UnspawnOutOfBoundsMechanics _unspawnOutOfBoundsMechanics;
        private UnspawnOnJumpedOnMechanics _unspawnOnJumpedOnMechanics;
        private ObstacleMoveMechanics _obstacleMoveMechanics;

      

        public void Init(ObstacleTypeEnum obstacleTypeEnum, GameManager gameManager, string spriteName, AnimationType animationType)
        {
            if (!ObstacleLaneResolver.TryResolveIsTop(transform.position.y, out bool isTop))
            {
                Debug.LogWarning(
                    $"Obstacle y={transform.position.y:F3} is not close to any known lane anchor. " +
                    $"Obstacle={obstacleTypeEnum}, sprite={spriteName}");
            }

            ObstacleType = new ObstacleType(isTop, obstacleTypeEnum);
            Hamster = LevelController.Instance.LevelData.Hamster;
            GameManager = gameManager;
            AnimationType = animationType;

            ObstacleId = $"{ObstacleType}_{spriteName}_{transform.position.x:F2}_{transform.position.y:F2}";
        }

        public void InitializeMechanics()
        {
            _scrollLeftMechanics = new ScrollLeftMechanics(transform, Consts.RoadScrollSpeed);
            _unspawnOutOfBoundsMechanics = new UnspawnOutOfBoundsMechanics(this, OnObstacleUnspawned);

            _boomEffect = new AtomicVariable<BoomEffect>(LevelController.Instance.LevelData.BoomEffectPrefab);
            BoomEffectAction = new BoomEffectAction(_boomEffect);

            _unspawnOnJumpedOnMechanics = new UnspawnOnJumpedOnMechanics(this);
            
            // Create additional movement mechanics for walking obstacles
            if (AnimationType == Common.Models.AnimationType.Walk)
            {
                _obstacleMoveMechanics = new ObstacleMoveMechanics(transform);
            }


            _unspawnOnJumpedOnMechanics.OnEnable();

            enabled = true;
        }

        public void OnUpdate(float deltaTime)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (_scrollLeftMechanics == null || _unspawnOutOfBoundsMechanics == null)
            {
                return;
            }

            _scrollLeftMechanics.Update(deltaTime);
            _obstacleMoveMechanics?.Update(deltaTime);
            _unspawnOutOfBoundsMechanics.Update();
        }

        public void OnPause()
        {
            enabled = false;
        }

        public void OnResume()
        {
            enabled = true;
        }

        private void OnDestroy()
        {
            _unspawnOnJumpedOnMechanics.OnDisable();
        }

        private float GetBoxColliderWidth()
        {
            var boxCollider2D = transform.GetComponentInChildren<BoxCollider2D>();
            if (boxCollider2D == null) throw new MissingComponentException("BoxCollider2D is missing on Obstacle object.");

            return boxCollider2D.size.x;
        }

        private float GetBoxColliderHeight()
        {
            var boxCollider2D = transform.GetComponentInChildren<BoxCollider2D>();
            if (boxCollider2D == null)
                throw new MissingComponentException("BoxCollider2D is missing on Obstacle object.");

            return boxCollider2D.size.y;
        }
    }
}
