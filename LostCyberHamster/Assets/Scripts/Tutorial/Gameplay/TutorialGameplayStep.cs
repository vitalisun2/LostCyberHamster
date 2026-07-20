using System;
using System.Collections.Generic;
using Assets.Scripts.Common.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Tutorial
{
    /// <summary>
    /// Неизменяемое описание одного gameplay-шага tutorial.
    /// </summary>
    public sealed class TutorialGameplayStep
    {
        private readonly IReadOnlyList<TutorialAction> _expectedActions;
        private readonly IReadOnlyList<ObstacleTypeEnum> _targetTypes;

        public TutorialGameplayStep(
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

        public TutorialGameplayStep(
            int number,
            string title,
            string instruction,
            IReadOnlyList<TutorialAction> expectedActions,
            float pauseDistance,
            HamsterStateEnum? completionState = null,
            params ObstacleTypeEnum[] targetTypes)
        {
            if (number <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(number));
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Tutorial step title is required.", nameof(title));
            }

            if (string.IsNullOrWhiteSpace(instruction))
            {
                throw new ArgumentException("Tutorial step instruction is required.", nameof(instruction));
            }

            if (expectedActions == null || expectedActions.Count == 0)
            {
                throw new ArgumentException("Tutorial step requires at least one action.", nameof(expectedActions));
            }

            if (pauseDistance < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(pauseDistance));
            }

            Number = number;
            Title = title;
            Instruction = instruction;
            PauseDistance = pauseDistance;
            CompletionState = completionState;
            _expectedActions = CopyActions(expectedActions);
            _targetTypes = Array.AsReadOnly(targetTypes == null
                ? Array.Empty<ObstacleTypeEnum>()
                : (ObstacleTypeEnum[])targetTypes.Clone());
        }

        public int Number { get; }
        public string Title { get; }
        public string Instruction { get; }
        public IReadOnlyList<TutorialAction> ExpectedActions => _expectedActions;
        public float PauseDistance { get; }
        public HamsterStateEnum? CompletionState { get; }
        public IReadOnlyList<ObstacleTypeEnum> TargetTypes => _targetTypes;

        private static IReadOnlyList<TutorialAction> CopyActions(IReadOnlyList<TutorialAction> actions)
        {
            var copy = new TutorialAction[actions.Count];
            for (int i = 0; i < actions.Count; i++)
            {
                copy[i] = actions[i];
            }

            return Array.AsReadOnly(copy);
        }
    }
}
