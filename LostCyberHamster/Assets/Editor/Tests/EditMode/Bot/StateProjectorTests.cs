using System.Collections.Generic;
using Assets.Scripts.Bot;
using Assets.Scripts.Common.Models;
using NUnit.Framework;

namespace Assets.Tests.EditMode.Bot
{
    /// <summary>
    /// Unit-тесты для StateProjector: проверяем корректность проекции
    /// каждого типа действия в ProjectedState.
    /// </summary>
    public class StateProjectorTests
    {
        private StateProjector _projector;

        // Константы из StateProjector (дублируются для читаемости тестов)
        private const float LaneSwitchTravel       = 0.3f * 3.8f; // ~1.14
        private const float JumpLandingTravel      = 3.8f;
        private const float SuperJumpLandingTravel = 4.6f;
        private const float JumpOnBounceTravel     = 3.5f;
        private const float Eps                    = 0.01f;

        [SetUp]
        public void SetUp()
        {
            _projector = new StateProjector();
        }

        // ──────────────── Вспомогательные ────────────────

        private static ProjectedState MakeState(
            float x = 0f, bool onBottom = true, bool onRoof = false,
            int energy = 100, int ulta = 0)
        {
            return new ProjectedState
            {
                ApproxX          = x,
                HamsterWidth     = 1.4f,
                OnBottom         = onBottom,
                OnRoof           = onRoof,
                Energy           = energy,
                UltaCharge       = ulta,
                RemainingObjects = new List<ObstacleInfo>()
            };
        }

        private static ObstacleInfo MakeObs(
            ObstacleTypeEnum type,
            float leftX, float rightX,
            bool isTopLane = false, bool isOnRoof = false,
            ObjectCategory category = ObjectCategory.Threat,
            int stableId = 1)
        {
            return new ObstacleInfo(
                type: type,
                leftX: leftX,
                rightX: rightX,
                centerX: (leftX + rightX) / 2f,
                isTopLane: isTopLane,
                isOnRoof: isOnRoof,
                distanceToHamster: leftX,
                timeToReach: 0f,
                category: category,
                obstacleRef: null,
                stableId: stableId,
                collectiblePriority: 0);
        }

        private static ChainStep MakeStep(BotAction action, int energyCost = 0,
            ObstacleInfo? target = null, string reason = "")
        {
            return new ChainStep(action, -1, 0f, energyCost, reason)
            {
                TargetObstacle = target
            };
        }

        // ══════════════════════════════════════════════
        //  SwitchLane
        // ══════════════════════════════════════════════

        [Test]
        public void SwitchLane_FlipsLane_DoesNotCostEnergy()
        {
            var state = MakeState(x: 0f, onBottom: true, energy: 50);
            var step  = MakeStep(BotAction.SwitchLane);

            var next = _projector.Project(state, step, null);

            Assert.IsFalse(next.OnBottom,  "SwitchLane должен сменить линию");
            Assert.AreEqual(50, next.Energy, "SwitchLane не должен тратить энергию");
            Assert.AreEqual(LaneSwitchTravel, next.ApproxX, Eps,
                "SwitchLane должен сдвинуть X на LaneSwitchTravel");
        }

        [Test]
        public void SwitchLane_Twice_RestoresOriginalLane()
        {
            var state  = MakeState(onBottom: true);
            var step   = MakeStep(BotAction.SwitchLane);
            var after1 = _projector.Project(state, step, null);
            var after2 = _projector.Project(after1, step, null);

            Assert.IsTrue(after2.OnBottom, "Двойная смена линии должна вернуть исходную");
        }

        // ══════════════════════════════════════════════
        //  Jump (JumpOver)
        // ══════════════════════════════════════════════

        [Test]
        public void Jump_NoTarget_AdvancessByJumpLandingTravel_Costs10Energy()
        {
            var state = MakeState(x: 0f, energy: 50);
            var step  = MakeStep(BotAction.Jump, energyCost: 10);

            var next = _projector.Project(state, step, null);

            Assert.AreEqual(JumpLandingTravel, next.ApproxX, Eps,
                "Jump без цели должен сдвинуть X на JumpLandingTravel");
            Assert.AreEqual(40, next.Energy, Eps, "Jump должен стоить 10 энергии");
        }

        [Test]
        public void SuperJump_NoTarget_AdvancessBySuperJumpLandingTravel_Costs20Energy()
        {
            var state = MakeState(x: 0f, energy: 50);
            var step  = MakeStep(BotAction.SuperJump, energyCost: 20);

            var next = _projector.Project(state, step, null);

            Assert.AreEqual(SuperJumpLandingTravel, next.ApproxX, Eps,
                "SuperJump без цели должен сдвинуть X на SuperJumpLandingTravel");
            Assert.AreEqual(30, next.Energy, Eps, "SuperJump должен стоить 20 энергии");
        }

        [Test]
        public void Jump_EnergyClampAtZero()
        {
            var state = MakeState(energy: 5);
            var step  = MakeStep(BotAction.Jump, energyCost: 10);

            var next = _projector.Project(state, step, null);

            Assert.AreEqual(0, next.Energy, "Энергия не может уйти ниже 0");
        }

        // ══════════════════════════════════════════════
        //  JumpOn SmallAlive
        // ══════════════════════════════════════════════

        [Test]
        public void Jump_OnSmallAlive_LandsAtRightXPlusBounceTravel()
        {
            var target = MakeObs(ObstacleTypeEnum.smallAlive, leftX: 2f, rightX: 3f,
                category: ObjectCategory.Target);
            var state  = MakeState(x: 0f, onBottom: true, energy: 50);
            var step   = MakeStep(BotAction.Jump, energyCost: 10, target: target, reason: "JumpOn smallAlive");

            var next = _projector.Project(state, step, target);

            Assert.AreEqual(target.RightX + JumpOnBounceTravel, next.ApproxX, Eps,
                "JumpOn SmallAlive: X = target.RightX + JumpOnBounceTravel");
            Assert.IsFalse(next.OnRoof, "JumpOn SmallAlive: хомяк остаётся на дороге");
            Assert.AreEqual(40, next.Energy, "JumpOn SmallAlive: 10 энергии");
        }

        // ══════════════════════════════════════════════
        //  Jump на крышу BigNotAlive
        // ══════════════════════════════════════════════

        [Test]
        public void Jump_OnBigNotAlive_SetsOnRoofTrue()
        {
            var target = MakeObs(ObstacleTypeEnum.bigNotAlive, leftX: 1f, rightX: 5f,
                isOnRoof: false, category: ObjectCategory.Threat);
            // Добавляем объект в RemainingObjects чтобы ResolveRoofAutoTransitions нашёл крышу
            target = new ObstacleInfo(
                type: ObstacleTypeEnum.bigNotAlive,
                leftX: 1f, rightX: 5f, centerX: 3f,
                isTopLane: false, isOnRoof: true,
                distanceToHamster: 1f, timeToReach: 0f,
                category: ObjectCategory.Threat,
                obstacleRef: null,
                stableId: 1, collectiblePriority: 0);

            var state = MakeState(x: 0f, onBottom: true, energy: 50);
            state.RemainingObjects.Add(target);
            var step = MakeStep(BotAction.Jump, energyCost: 10, target: target);

            var next = _projector.Project(state, step, target);

            Assert.IsTrue(next.OnRoof, "Прыжок на BigNotAlive должен установить OnRoof=true");
            Assert.AreEqual(40, next.Energy, "Прыжок на крышу стоит 10 энергии");
        }

        // ══════════════════════════════════════════════
        //  SuperJump на BigAlive
        // ══════════════════════════════════════════════

        [Test]
        public void SuperJump_OverBigAlive_AdvancesBySuperJumpLandingTravel()
        {
            var target = MakeObs(ObstacleTypeEnum.bigAlive, leftX: 2f, rightX: 4f,
                category: ObjectCategory.Threat);
            var state  = MakeState(x: 0f, energy: 50);
            var step   = MakeStep(BotAction.SuperJump, energyCost: 20, target: target);

            var next = _projector.Project(state, step, target);

            // JumpOver: target.RightX + travel * 0.4f
            float expectedX = target.RightX + SuperJumpLandingTravel * 0.4f;
            Assert.AreEqual(expectedX, next.ApproxX, Eps,
                "SuperJump над BigAlive: X = target.RightX + SuperJumpLandingTravel * 0.4");
            Assert.AreEqual(30, next.Energy, "SuperJump стоит 20 энергии");
        }

        // ══════════════════════════════════════════════
        //  UseUlta
        // ══════════════════════════════════════════════

        [Test]
        public void UseUlta_ClearsUltaCharge_RemovesNearThreats()
        {
            var threat1 = MakeObs(ObstacleTypeEnum.bigAlive, leftX: 1f, rightX: 2f,
                category: ObjectCategory.Threat, stableId: 1);
            var threat2 = MakeObs(ObstacleTypeEnum.bigAlive, leftX: 3f, rightX: 4f,
                category: ObjectCategory.Threat, stableId: 2);
            var farThreat = MakeObs(ObstacleTypeEnum.bigAlive, leftX: 10f, rightX: 11f,
                category: ObjectCategory.Threat, stableId: 3);

            var state = MakeState(x: 0f, ulta: 100);
            state.RemainingObjects.AddRange(new[] { threat1, threat2, farThreat });

            var step = MakeStep(BotAction.UseUlta);
            var next = _projector.Project(state, step, null);

            Assert.AreEqual(0, next.UltaCharge, "Ульта должна обнулить заряд");
            Assert.AreEqual(1, next.RemainingObjects.Count,
                "Ульта должна удалить ближние угрозы, оставив дальние");
            Assert.AreEqual(10f, next.RemainingObjects[0].LeftX, Eps,
                "Дальняя угроза (>6f) должна остаться");
        }

        // ══════════════════════════════════════════════
        //  IsSafeAfterProjection
        // ══════════════════════════════════════════════

        [Test]
        public void IsSafeAfterProjection_NearThreatSameLane_ReturnsFalse()
        {
            var threat = MakeObs(ObstacleTypeEnum.bigAlive, leftX: -0.3f, rightX: 0.3f,
                isTopLane: false, isOnRoof: false, category: ObjectCategory.Threat);

            var state = MakeState(x: 0f, onBottom: true);
            state.RemainingObjects.Add(threat);

            Assert.IsFalse(_projector.IsSafeAfterProjection(state),
                "Угроза прямо на хомяке — небезопасно");
        }

        [Test]
        public void IsSafeAfterProjection_ThreatOtherLane_ReturnsTrue()
        {
            // Угроза на верхней линии, хомяк на нижней
            var threat = MakeObs(ObstacleTypeEnum.bigAlive, leftX: -0.3f, rightX: 0.3f,
                isTopLane: true, isOnRoof: false, category: ObjectCategory.Threat);

            var state = MakeState(x: 0f, onBottom: true);
            state.RemainingObjects.Add(threat);

            Assert.IsTrue(_projector.IsSafeAfterProjection(state),
                "Угроза на другой линии — безопасно");
        }

        [Test]
        public void IsSafeAfterProjection_NoThreats_ReturnsTrue()
        {
            var state = MakeState(x: 5f);
            Assert.IsTrue(_projector.IsSafeAfterProjection(state),
                "Нет угроз — безопасно");
        }
    }
}
