using System;
using Assets.Scripts.Common.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Описание одного тестового сценария из hamster_collision_test_scenarios.md.
    /// Группы: JumpMechanics, RoofJumpMechanics, RoofRunMechanics, SuperJumpMechanics, SuperRoofJumpMechanics.
    /// </summary>
    [Serializable]
    public class BotTestScenario
    {
        public string Group;
        public int ScenarioId;
        public string Description;
        public HamsterStateEnum ExpectedState;
        public InteractionType Interaction;
        public ObstacleTypeEnum TargetObstacleType;
        public TestStatus Status;
        public string ResultComment;
        public float TestedAtTimestamp;

        public BotTestScenario(
            string group,
            int scenarioId,
            string description,
            HamsterStateEnum expectedState,
            InteractionType interaction,
            ObstacleTypeEnum targetObstacleType = ObstacleTypeEnum.smallAlive)
        {
            Group = group;
            ScenarioId = scenarioId;
            Description = description;
            ExpectedState = expectedState;
            Interaction = interaction;
            TargetObstacleType = targetObstacleType;
            Status = TestStatus.NotTested;
            ResultComment = "";
        }

        public void MarkPassed(float timestamp, string comment = "")
        {
            Status = TestStatus.Pass;
            TestedAtTimestamp = timestamp;
            ResultComment = comment;
        }

        public void MarkFailed(float timestamp, string comment)
        {
            Status = TestStatus.Fail;
            TestedAtTimestamp = timestamp;
            ResultComment = comment;
        }

        public void MarkSkipped(string reason)
        {
            Status = TestStatus.Skip;
            ResultComment = reason;
        }

        public override string ToString()
        {
            string statusIcon = Status switch
            {
                TestStatus.Pass => "PASS",
                TestStatus.Fail => "FAIL",
                TestStatus.Skip => "SKIP",
                _ => "----"
            };
            return $"[{statusIcon}] {Group} #{ScenarioId}: {Description} " +
                   $"(expected={ExpectedState}, obstacle={TargetObstacleType}, interaction={Interaction})" +
                   (string.IsNullOrEmpty(ResultComment) ? "" : $" // {ResultComment}");
        }
    }

    public enum InteractionType
    {
        JumpOn,
        JumpOver,
        Collision,
        RoofJump,
        RoofRun,
        RunFromRoof,
        SuperJumpOn,
        SuperJumpOver,
        SuperJumpCollision,
        LandOnRoof,
        FallFromRoof,
        NoContact
    }

    public enum TestStatus
    {
        NotTested,
        Pass,
        Fail,
        Skip
    }
}
