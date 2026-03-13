using System.Collections.Generic;
using Assets.Scripts.Bot;
using Assets.Scripts.Common.Models;
using NUnit.Framework;

namespace Assets.Tests.EditMode.Bot
{
    /// <summary>
    /// Unit-тесты для PlanValidator: три ключевых сценария из спецификации.
    /// </summary>
    public class PlanValidatorTests
    {
        private PlanValidator _validator;

        [SetUp]
        public void SetUp()
        {
            _validator = new PlanValidator();
        }

        // ──────────────── Вспомогательные ────────────────

        /// <summary>Создаёт минимальный снимок сцены. Хомяк на нижней линии.</summary>
        private static BotSceneSnapshot MakeSnapshot(
            List<ObstacleInfo> visible = null,
            bool onRoof = false,
            float hamsterRightX = 0f,
            int energy = 100)
        {
            return new BotSceneSnapshot
            {
                HamsterOnBottom  = !onRoof,
                HamsterOnRoof    = onRoof,
                HamsterRightX    = hamsterRightX,
                Energy           = energy,
                VisibleObjects   = visible ?? new List<ObstacleInfo>()
            };
        }

        /// <summary>Строит препятствие с указанным stableId и дистанцией.</summary>
        private static ObstacleInfo MakeObs(
            int stableId,
            float distanceToHamster,
            bool isOnRoof = false,
            bool isTopLane = false,
            ObjectCategory category = ObjectCategory.Threat)
        {
            float leftX = distanceToHamster;
            float rightX = distanceToHamster + 1f;
            return new ObstacleInfo(
                type: ObstacleTypeEnum.smallAlive,
                leftX: leftX, rightX: rightX,
                centerX: leftX + 0.5f,
                isTopLane: isTopLane,
                isOnRoof: isOnRoof,
                distanceToHamster: distanceToHamster,
                timeToReach: 0f,
                category: category,
                obstacleRef: null,
                stableId: stableId,
                collectiblePriority: 0);
        }

        /// <summary>Создаёт шаг без конкретной цели (SwitchLane).</summary>
        private static ChainStep MakeSwitchStep()
        {
            return new ChainStep(
                action: BotAction.SwitchLane,
                targetObstacleIndex: -1,
                executeAtDistance: 0f,
                energyCost: 0,
                reason: "test switch");
        }

        /// <summary>Создаёт шаг, нацеленный на конкретное препятствие.</summary>
        private static ChainStep MakeTargetStep(ObstacleInfo target)
        {
            return new ChainStep(
                action: BotAction.Jump,
                targetObstacleIndex: 0,
                executeAtDistance: target.DistanceToHamster,
                energyCost: 10,
                reason: "test jump")
            { TargetObstacle = target };
        }

        /// <summary>Формирует план: шаг Head + список шагов хвоста.</summary>
        private static CurrentPlan MakePlan(ChainStep head, params ChainStep[] tail)
        {
            var plan = new CurrentPlan { Strategy = "test" };
            plan.Steps.Add(head);
            foreach (var s in tail)
                plan.Steps.Add(s);
            return plan;
        }

        // ══════════════════════════════════════════════
        //  1. Пустой план → FullRebuild
        // ══════════════════════════════════════════════

        [Test]
        public void Validate_EmptyPlan_ReturnsFullRebuild()
        {
            var snapshot = MakeSnapshot();
            var emptyPlan = new CurrentPlan();

            var decision = _validator.Validate(snapshot, emptyPlan);

            Assert.AreEqual(PlanDecision.FullRebuild, decision,
                "Пустой план должен вернуть FullRebuild");
        }

        // ══════════════════════════════════════════════
        //  2. Цель хвоста исчезла → FullRebuild
        // ══════════════════════════════════════════════

        [Test]
        public void Validate_TailTargetDisappeared_ReturnsFullRebuild()
        {
            // Целевой объект (stableId=5) отсутствует в снимке
            var targetObs = MakeObs(stableId: 5, distanceToHamster: 8f, category: ObjectCategory.Threat);
            var snapshot = MakeSnapshot(visible: new List<ObstacleInfo>()); // объектов нет

            var plan = MakePlan(
                head: MakeSwitchStep(),
                tail: MakeTargetStep(targetObs));   // хвост ссылается на StableId=5

            var decision = _validator.Validate(snapshot, plan);

            Assert.AreEqual(PlanDecision.FullRebuild, decision,
                "Если цель хвоста исчезла — необходим FullRebuild");
        }

        // ══════════════════════════════════════════════
        //  3. Новая угроза блокирует путь к первому шагу хвоста → FullRebuild
        // ══════════════════════════════════════════════

        [Test]
        public void Validate_NewThreatBlocksPath_ReturnsFullRebuild()
        {
            // Цель хвоста: видима, дистанция 8.0
            var targetObs = MakeObs(stableId: 5, distanceToHamster: 8f,
                isOnRoof: false, category: ObjectCategory.Neutral);
            // Новая угроза: на той же нижней линии, на расстоянии 3.0 (< 8 + 0.5)
            var blockingThreat = MakeObs(stableId: 99, distanceToHamster: 3f,
                isOnRoof: false, category: ObjectCategory.Threat);

            var snapshot = MakeSnapshot(
                visible: new List<ObstacleInfo> { targetObs, blockingThreat },
                onRoof: false);

            var plan = MakePlan(
                head: MakeSwitchStep(),
                tail: MakeTargetStep(targetObs));

            var decision = _validator.Validate(snapshot, plan);

            Assert.AreEqual(PlanDecision.FullRebuild, decision,
                "Новая угроза по пути к хвосту должна вызвать FullRebuild");
        }

        // ══════════════════════════════════════════════
        //  4. Ничего не изменилось → KeepTail
        // ══════════════════════════════════════════════

        [Test]
        public void Validate_NothingChanged_ReturnsKeepTail()
        {
            // Хвост: SwitchLane без цели — не требует, чтобы объекты оставались видимыми,
            // и репроекция безопасна при пустой сцене
            var snapshot = MakeSnapshot(
                visible: new List<ObstacleInfo>(), // нет угроз
                onRoof: false,
                hamsterRightX: 0f,
                energy: 100);

            var plan = MakePlan(
                head: MakeSwitchStep(),
                tail: MakeSwitchStep());   // хвост: один SwitchLane без цели

            var decision = _validator.Validate(snapshot, plan);

            Assert.AreEqual(PlanDecision.KeepTail, decision,
                "Без изменений план должен быть сохранён (KeepTail)");
        }

        // ══════════════════════════════════════════════
        //  5. Угроза на другой линии не блокирует путь
        // ══════════════════════════════════════════════

        [Test]
        public void Validate_ThreatOnOtherLane_DoesNotBlock_ReturnsKeepTail()
        {
            // Цель хвоста: видима, дистанция 8.0, нижняя линия
            var targetObs = MakeObs(stableId: 5, distanceToHamster: 8f,
                isOnRoof: false, category: ObjectCategory.Neutral);
            // Угроза на крыше — хомяк внизу, угроза не на его пути
            var roofThreat = MakeObs(stableId: 77, distanceToHamster: 3f,
                isOnRoof: true, category: ObjectCategory.Threat);

            var snapshot = MakeSnapshot(
                visible: new List<ObstacleInfo> { targetObs, roofThreat },
                onRoof: false);

            var plan = MakePlan(
                head: MakeSwitchStep(),
                tail: MakeTargetStep(targetObs));

            var decision = _validator.Validate(snapshot, plan);

            Assert.AreEqual(PlanDecision.KeepTail, decision,
                "Угроза на крыше не должна блокировать путь хомяка внизу");
        }
    }
}
