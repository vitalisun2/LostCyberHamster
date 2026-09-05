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

        /// <summary>Создаёт шаг с одним действием и ключами локализованных подсказок.</summary>
        public TutorialGameplayStep(
            int number,
            string titleKey,
            string instructionKey,
            TutorialAction expectedAction,
            float pauseDistance,
            HamsterStateEnum? completionState = null,
            params ObstacleTypeEnum[] targetTypes)
            : this(
                number,
                titleKey,
                instructionKey,
                new[] { expectedAction },
                pauseDistance,
                completionState,
                targetTypes)
        {
        }

        /// <summary>Создаёт шаг с последовательностью действий и ключами локализованных подсказок.</summary>
        public TutorialGameplayStep(
            int number,
            string titleKey,
            string instructionKey,
            IReadOnlyList<TutorialAction> expectedActions,
            float pauseDistance,
            HamsterStateEnum? completionState = null,
            params ObstacleTypeEnum[] targetTypes)
        {
            // Проверяет обязательный контракт шага.
            if (number <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(number));
            }

            if (string.IsNullOrWhiteSpace(titleKey))
            {
                throw new ArgumentException("Tutorial step title key is required.", nameof(titleKey));
            }

            if (string.IsNullOrWhiteSpace(instructionKey))
            {
                throw new ArgumentException("Tutorial step instruction key is required.", nameof(instructionKey));
            }

            if (expectedActions == null || expectedActions.Count == 0)
            {
                throw new ArgumentException("Tutorial step requires at least one action.", nameof(expectedActions));
            }

            if (pauseDistance < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(pauseDistance));
            }

            // Сохраняет неизменяемое описание сценария.
            Number = number;
            TitleKey = titleKey;
            InstructionKey = instructionKey;
            PauseDistance = pauseDistance;
            CompletionState = completionState;
            _expectedActions = CopyActions(expectedActions);
            _targetTypes = Array.AsReadOnly(targetTypes == null
                ? Array.Empty<ObstacleTypeEnum>()
                : (ObstacleTypeEnum[])targetTypes.Clone());
        }

        public int Number { get; }
        public string TitleKey { get; }
        public string InstructionKey { get; }
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
