using System;
using System.Collections.Generic;
using Assets.Scripts.Common.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Tutorial
{
    /// <summary>
    /// Единственный владелец состава gameplay-шагов tutorial.
    /// </summary>
    public static class TutorialGameplayStepCatalog
    {
        private static readonly IReadOnlyList<TutorialGameplayStep> _coreControls = Array.AsReadOnly(new[]
        {
            new TutorialGameplayStep(
                number: 1,
                titleKey: "tutorial_step_1_title",
                instructionKey: "tutorial_step_1_instruction",
                expectedAction: TutorialAction.Tap,
                pauseDistance: 4.2f,
                targetTypes: ObstacleTypeEnum.smallNotAliveRoad),
            new TutorialGameplayStep(
                number: 2,
                titleKey: "tutorial_step_2_title",
                instructionKey: "tutorial_step_2_instruction",
                expectedAction: TutorialAction.Jump,
                pauseDistance: 0.45f,
                targetTypes: ObstacleTypeEnum.smallNotAliveRoad),
            new TutorialGameplayStep(
                number: 3,
                titleKey: "tutorial_step_3_title",
                instructionKey: "tutorial_step_3_instruction",
                expectedAction: TutorialAction.Jump,
                pauseDistance: 1.9f,
                targetTypes: ObstacleTypeEnum.smallAlive),
            new TutorialGameplayStep(
                number: 4,
                titleKey: "tutorial_step_4_title",
                instructionKey: "tutorial_step_4_instruction",
                expectedActions: new[] { TutorialAction.Jump, TutorialAction.SuperJump },
                pauseDistance: 1.55f,
                targetTypes: ObstacleTypeEnum.bigAlive),
            new TutorialGameplayStep(
                number: 5,
                titleKey: "tutorial_step_5_title",
                instructionKey: "tutorial_step_5_instruction",
                expectedActions: new[] { TutorialAction.Jump, TutorialAction.SuperJump },
                pauseDistance: 3.2f,
                targetTypes: ObstacleTypeEnum.smallAlive),
            new TutorialGameplayStep(
                number: 6,
                titleKey: "tutorial_step_6_title",
                instructionKey: "tutorial_step_6_instruction",
                expectedAction: TutorialAction.Jump,
                pauseDistance: 2.4f,
                completionState: HamsterStateEnum.RoofRun,
                targetTypes: new[] { ObstacleTypeEnum.bigNotAlive, ObstacleTypeEnum.mediumNotAlive }),
            new TutorialGameplayStep(
                number: 7,
                titleKey: "tutorial_step_7_title",
                instructionKey: "tutorial_step_7_instruction",
                expectedAction: TutorialAction.Jump,
                pauseDistance: 3.2f,
                completionState: HamsterStateEnum.RoofRun,
                targetTypes: new[] { ObstacleTypeEnum.bigNotAlive, ObstacleTypeEnum.mediumNotAlive }),
            new TutorialGameplayStep(
                number: 8,
                titleKey: "tutorial_step_8_title",
                instructionKey: "tutorial_step_8_instruction",
                expectedAction: TutorialAction.Jump,
                pauseDistance: 3.9f,
                targetTypes: ObstacleTypeEnum.smallAlive)
        });

        public static IReadOnlyList<TutorialGameplayStep> Steps => _coreControls;
    }
}
