using System;
using System.Collections.Generic;
using Assets.Scripts.Common.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Tutorial
{
    /// <summary>
    /// Единственный владелец состава gameplay-сценариев tutorial.
    /// </summary>
    public static class TutorialGameplayStepCatalog
    {
        private static readonly IReadOnlyList<TutorialGameplayStep> _coreControls = Array.AsReadOnly(new[]
        {
            new TutorialGameplayStep(
                number: 1,
                title: "Обучение 1 - уклониться",
                instruction: "Тапни, чтобы увернуться",
                expectedAction: TutorialAction.Tap,
                pauseDistance: 4.2f,
                targetTypes: ObstacleTypeEnum.smallNotAliveRoad),
            new TutorialGameplayStep(
                number: 2,
                title: "Обучение 2 - перепрыгнуть",
                instruction: "Нажми прыжок, чтобы перепрыгнуть",
                expectedAction: TutorialAction.Jump,
                pauseDistance: 0.45f,
                targetTypes: ObstacleTypeEnum.smallNotAliveRoad),
            new TutorialGameplayStep(
                number: 3,
                title: "Обучение 3 - запрыгнуть",
                instruction: "Прыгни на препятствие",
                expectedAction: TutorialAction.Jump,
                pauseDistance: 1.9f,
                targetTypes: ObstacleTypeEnum.smallAlive),
            new TutorialGameplayStep(
                number: 4,
                title: "Обучение 4 - суперпрыжок",
                instruction: "Нажми прыжок дважды, чтобы перелететь дальше",
                expectedActions: new[] { TutorialAction.Jump, TutorialAction.SuperJump },
                pauseDistance: 1.55f,
                targetTypes: ObstacleTypeEnum.bigAlive),
            new TutorialGameplayStep(
                number: 5,
                title: "Обучение 5 - супернапрыг",
                instruction: "Нажми прыжок дважды и приземлись сверху",
                expectedActions: new[] { TutorialAction.Jump, TutorialAction.SuperJump },
                pauseDistance: 3.2f,
                targetTypes: ObstacleTypeEnum.smallAlive),
            new TutorialGameplayStep(
                number: 6,
                title: "Обучение 6 - запрыгнуть на крышу",
                instruction: "Прыгни на крышу",
                expectedAction: TutorialAction.Jump,
                pauseDistance: 2.4f,
                completionState: HamsterStateEnum.RoofRun,
                targetTypes: new[] { ObstacleTypeEnum.bigNotAlive, ObstacleTypeEnum.mediumNotAlive }),
            new TutorialGameplayStep(
                number: 7,
                title: "Обучение 7 - крыша к крыше",
                instruction: "Прыгни на следующую крышу",
                expectedAction: TutorialAction.Jump,
                pauseDistance: 3.2f,
                completionState: HamsterStateEnum.RoofRun,
                targetTypes: new[] { ObstacleTypeEnum.bigNotAlive, ObstacleTypeEnum.mediumNotAlive }),
            new TutorialGameplayStep(
                number: 8,
                title: "Обучение 8 - напрыгнуть с крыши",
                instruction: "Прыгни с крыши на препятствие",
                expectedAction: TutorialAction.Jump,
                pauseDistance: 3.9f,
                targetTypes: ObstacleTypeEnum.smallAlive)
        });

        private static readonly IReadOnlyList<TutorialGameplayStep> _superHit = Array.AsReadOnly(new[]
        {
            new TutorialGameplayStep(
                number: 10,
                title: "Обучение 10 - суперудар",
                instruction: "Используйте суперудар",
                expectedAction: TutorialAction.Ultra,
                pauseDistance: 7.2f,
                targetTypes: new[]
                {
                    ObstacleTypeEnum.smallNotAliveRoad,
                    ObstacleTypeEnum.bigAlive,
                    ObstacleTypeEnum.smallAlive
                })
        });

        public static IReadOnlyList<TutorialGameplayStep> GetSteps(TutorialGameplayScenario scenario)
        {
            switch (scenario)
            {
                case TutorialGameplayScenario.CoreControls:
                    return _coreControls;
                case TutorialGameplayScenario.SuperHit:
                    return _superHit;
                default:
                    throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
            }
        }
    }
}
