using System.Collections.Generic;
using Assets.Scripts.Bot;
using Assets.Scripts.Common.Models;
using NUnit.Framework;

namespace Assets.Tests.EditMode.Bot
{
    /// <summary>
    /// Unit-тесты для ChainGenerator: проверяем что генератор
    /// создаёт правильные наборы кандидатных цепочек.
    /// </summary>
    public class ChainGeneratorTests
    {
        private ChainGenerator _generator;
        private StateProjector _projector;

        [SetUp]
        public void SetUp()
        {
            _generator = new ChainGenerator();
            _projector = new StateProjector();
        }

        // ──────────────── Вспомогательные ────────────────

        private static ProjectedState MakeState(
            float x = 0f, bool onBottom = true, int energy = 100)
        {
            return new ProjectedState
            {
                ApproxX          = x,
                OnBottom         = onBottom,
                OnRoof           = false,
                Energy           = energy,
                UltaCharge       = 0,
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
                leftX: leftX, rightX: rightX,
                centerX: (leftX + rightX) / 2f,
                isTopLane: isTopLane, isOnRoof: isOnRoof,
                distanceToHamster: leftX,
                timeToReach: 0f,
                type: type,
                stableId: stableId,
                collectiblePriority: 0)
            { Category = category };
        }

        // ══════════════════════════════════════════════
        //  Пустая сцена
        // ══════════════════════════════════════════════

        [Test]
        public void Generate_EmptyScene_ReturnsSingleEmptyCandidate()
        {
            var state = MakeState();
            var candidates = _generator.Generate(new List<ObstacleInfo>(), state);

            Assert.IsNotNull(candidates);
            Assert.IsTrue(candidates.Count >= 1, "Должен быть хотя бы 1 кандидат (пустая цепочка)");
            Assert.AreEqual(0, candidates[0].Steps.Count,
                "Для пустой сцены кандидат — пустая цепочка");
        }

        // ══════════════════════════════════════════════
        //  SmallAlive впереди
        // ══════════════════════════════════════════════

        [Test]
        public void Generate_OneSmallAliveAhead_AtLeastTwoVariants()
        {
            var obs = MakeObs(ObstacleTypeEnum.smallAlive, leftX: 2f, rightX: 3f,
                isTopLane: false, category: ObjectCategory.Threat);

            var state = MakeState();
            var candidates = _generator.Generate(new List<ObstacleInfo> { obs }, state);

            Assert.GreaterOrEqual(candidates.Count, 2,
                "SmallAlive: минимум 2 кандидата (SwitchLane + прыжок)");

            // Хотя бы один кандидат содержит SwitchLane
            bool hasSwitchLane = candidates.Exists(c =>
                c.Steps.Exists(s => s.Action == BotAction.SwitchLane));
            Assert.IsTrue(hasSwitchLane, "Должен быть вариант с SwitchLane");
        }

        // ══════════════════════════════════════════════
        //  Нет энергии
        // ══════════════════════════════════════════════

        [Test]
        public void Generate_ZeroEnergy_OnlySwitchLaneVariants()
        {
            var obs = MakeObs(ObstacleTypeEnum.smallAlive, leftX: 2f, rightX: 3f,
                isTopLane: false, category: ObjectCategory.Threat);

            var state = MakeState(energy: 0);
            var candidates = _generator.Generate(new List<ObstacleInfo> { obs }, state);

            // При нулевой энергии прыжки недоступны — только SwitchLane
            bool hasJump = candidates.Exists(c =>
                c.Steps.Exists(s =>
                    s.Action == BotAction.Jump ||
                    s.Action == BotAction.SuperJump ||
                    s.Action == BotAction.RoofJump));
            Assert.IsFalse(hasJump,
                "При нулевой энергии не должно быть прыжков");
        }

        // ══════════════════════════════════════════════
        //  Ульта (2 угрозы рядом + заряд 100)
        // ══════════════════════════════════════════════

        [Test]
        public void Generate_UltaReady_TwoThreatsNearby_HasUltaCandidate()
        {
            var threat1 = MakeObs(ObstacleTypeEnum.bigAlive, leftX: 1f, rightX: 2f,
                isTopLane: false, category: ObjectCategory.Threat, stableId: 1);
            var threat2 = MakeObs(ObstacleTypeEnum.bigAlive, leftX: 3f, rightX: 4f,
                isTopLane: false, category: ObjectCategory.Threat, stableId: 2);

            var state = MakeState(energy: 100);
            state.UltaCharge = 100;
            state.RemainingObjects.Add(threat1);
            state.RemainingObjects.Add(threat2);

            var objects = new List<ObstacleInfo> { threat1, threat2 };
            var candidates = _generator.Generate(objects, state);

            bool hasUlta = candidates.Exists(c =>
                c.Steps.Count > 0 && c.Steps[0].Action == BotAction.UseUlta);
            Assert.IsTrue(hasUlta, "При заряженной ульте и 2 угрозах должен быть вариант UseUlta");
        }

        // ══════════════════════════════════════════════
        //  Кандидаты AllStepsSafe
        // ══════════════════════════════════════════════

        [Test]
        public void Generate_AllCandidates_AreMarkedSafe()
        {
            var obs = MakeObs(ObstacleTypeEnum.smallNotAliveRoad, leftX: 3f, rightX: 4f,
                isTopLane: false, category: ObjectCategory.Threat);

            var state = MakeState();
            var candidates = _generator.Generate(new List<ObstacleInfo> { obs }, state);

            foreach (var c in candidates)
            {
                Assert.IsTrue(c.AllStepsSafe,
                    $"Все кандидаты из генератора должны быть AllStepsSafe=true, " +
                    $"нарушение: {c.Strategy}");
            }
        }

        // ══════════════════════════════════════════════
        //  Лимит кандидатов
        // ══════════════════════════════════════════════

        [Test]
        public void Generate_ManyObjects_DoesNotExceedMaxCandidates()
        {
            var objects = new List<ObstacleInfo>();
            for (int i = 0; i < 10; i++)
            {
                objects.Add(MakeObs(ObstacleTypeEnum.smallAlive,
                    leftX: 2f + i * 4f, rightX: 3f + i * 4f,
                    isTopLane: false, category: ObjectCategory.Threat, stableId: i + 1));
            }

            var state = MakeState();
            var candidates = _generator.Generate(objects, state);

            Assert.LessOrEqual(candidates.Count, 50,
                "Генератор не должен превышать лимит 50 кандидатов");
        }
    }
}
