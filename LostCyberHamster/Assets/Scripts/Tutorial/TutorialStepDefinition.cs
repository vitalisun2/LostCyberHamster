using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Common.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Tutorial
{
    /// <summary>
    /// Описание одного шага обучения.
    /// </summary>
    public sealed class TutorialStepDefinition
    {
        public TutorialStepDefinition(
            int number,
            string title,
            string instruction,
            TutorialAction expectedAction,
            float pauseDistance,
            HamsterStateEnum? completionState = null,
            params ObstacleTypeEnum[] targetTypes)
            : this(
                number,
                title,
                instruction,
                new[] { expectedAction },
                pauseDistance,
                completionState,
                targetTypes)
        {
        }

        public TutorialStepDefinition(
            int number,
            string title,
            string instruction,
            IReadOnlyList<TutorialAction> expectedActions,
            float pauseDistance,
            HamsterStateEnum? completionState = null,
            params ObstacleTypeEnum[] targetTypes)
        {
            Number = number;
            Title = title;
            Instruction = instruction;
            ExpectedActions = expectedActions?.ToArray() ?? new[] { TutorialAction.Tap };
            PauseDistance = pauseDistance;
            CompletionState = completionState;
            TargetTypes = targetTypes?.ToArray() ?? new ObstacleTypeEnum[0];
        }

        public int Number { get; }
        public string Title { get; }
        public string Instruction { get; }
        public IReadOnlyList<TutorialAction> ExpectedActions { get; }
        public float PauseDistance { get; }
        public HamsterStateEnum? CompletionState { get; }
        public IReadOnlyList<ObstacleTypeEnum> TargetTypes { get; }
    }
}
