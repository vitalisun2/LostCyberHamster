using Assets.Scripts.Common.Models;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Atomic.Elements;
using NUnit.Framework;
using UnityEngine;

namespace Assets.Tests.EditMode
{
    public sealed class ShieldJumpDamageTests
    {
        [Test]
        public void ResolveJump_RoofHazardDamage_TracksHazardAsDamageSource()
        {
            JumpObstacleData[] obstacles =
            {
                new(ObstacleTypeEnum.bigNotAlive, true, 0.6f, 1.2f, 0.9f),
                new(ObstacleTypeEnum.smallNotAliveRoadAndRoof, true, 0.7f, 0.9f, 0.8f),
            };
            JumpResolveContext context = new(
                isBottomLine: true,
                hamsterLeftX: 0f,
                hamsterRightX: 0.6f,
                hamsterCenterX: 0.3f,
                hamsterWidth: 0.6f,
                jumpShift: 0.2f,
                reachShift: 0.2f);

            JumpResolveResult result = JumpOutcomeResolver.ResolveJump(obstacles, context);

            Assert.AreEqual(HamsterStateEnum.JumpOnRoofDamage, result.State);
            Assert.AreEqual(0, result.TargetIndex);
            Assert.AreEqual(1, result.DamageSourceIndex);
        }

        [Test]
        public void JumpMid_ShieldedDamageState_PublishesProtectedContactInsteadOfDamage()
        {
            GameObject dispatcherObject = new("dispatcher");
            GameObject obstacleObject = new("obstacle");

            try
            {
                var dispatcher = dispatcherObject.AddComponent<TransformAnimatorEventsDispatcher>();
                var obstacle = obstacleObject.AddComponent<Obstacle>();

                var hamsterState = new AtomicVariable<HamsterStateEnum>(HamsterStateEnum.JumpDamageForBigAlive);
                var needCheckCollisionInRunFromRoofAfterShift = new AtomicVariable<bool>(false);
                var jumpOverEvent = new AtomicEvent();
                var destroyObstacleEvent = new AtomicEvent<Obstacle>();
                var pendingJumpedOnObstacle = new AtomicVariable<Obstacle>(null);
                var pendingDamageObstacle = new AtomicVariable<Obstacle>(obstacle);

                var isProtected = new AtomicVariable<bool>(true);
                var isSuperAttackDestructiveOnCollision = new AtomicVariable<bool>(false);
                var protectedContactEvent = new AtomicEvent<Obstacle>();
                var damageEvent = new AtomicEvent();
                var destroyObstacleBySuperAttackEvent = new AtomicEvent<Obstacle>();
                var gate = new ObstacleDamageGate(
                    isProtected,
                    isSuperAttackDestructiveOnCollision,
                    protectedContactEvent,
                    damageEvent,
                    destroyObstacleBySuperAttackEvent);

                int protectedContacts = 0;
                int damageCount = 0;
                protectedContactEvent.Subscribe(_ => protectedContacts++);
                damageEvent.Subscribe(() => damageCount++);

                var mechanics = new HamsterAnimationEventsMechanics(
                    dispatcher,
                    spriteAnimatorController: null,
                    hamsterState,
                    needCheckCollisionInRunFromRoofAfterShift,
                    jumpOverEvent,
                    destroyObstacleEvent,
                    destroyObstacleBySuperAttackEvent,
                    pendingJumpedOnObstacle,
                    pendingDamageObstacle,
                    gate);

                mechanics.OnEnable();
                dispatcher.ReceiveEvent("transform_jump_mid");
                mechanics.OnDisable();

                Assert.AreEqual(1, protectedContacts);
                Assert.AreEqual(0, damageCount);
                Assert.IsNull(pendingDamageObstacle.Value);
            }
            finally
            {
                Object.DestroyImmediate(obstacleObject);
                Object.DestroyImmediate(dispatcherObject);
            }
        }
    }
}