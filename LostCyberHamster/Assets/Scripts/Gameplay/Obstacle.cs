using Assets.Scripts.Common.Models;
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
            var computedIsTop = IsPositionOnTopLine(transform.position.y);
            ObstacleType = new ObstacleType(computedIsTop, obstacleTypeEnum);
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

        private bool IsPositionOnTopLine(float yPos)
        {
            // расстояние до любой верхней линии (дорога + крыша)
            var diffTop = Mathf.Min(
                Mathf.Abs(yPos - Consts.ObstacleY0Pos),
                Mathf.Abs(yPos - Consts.ObstacleRoofY0Pos));

            // расстояние до любой нижней линии (дорога + крыша)
            var diffBottom = Mathf.Min(
                Mathf.Abs(yPos - Consts.ObstacleY1Pos),
                Mathf.Abs(yPos - Consts.ObstacleRoofY1Pos));

            // верхняя, если ближе к верхним линиям и в пределах tolerance
            return diffTop <= diffBottom && diffTop <= Consts.ObstacleLineTolerance;
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
