using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpOn
{
    /// <summary>
    /// Хранит дистанции ground jump-on: отдельно runtime resolver-точку и полный action до возврата в Run.
    /// </summary>
    internal readonly struct JumpOnTravel
    {
        public JumpOnTravel(
            float actionTravel,
            float resolveTravel,
            float resolveFireShiftOffset)
        {
            ActionTravel = actionTravel;
            ResolveTravel = resolveTravel;
            ResolveFireShiftOffset = resolveFireShiftOffset;
        }

        /// <summary>
        /// Дистанция мира до полного завершения action и возврата в Run.
        /// </summary>
        public float ActionTravel { get; }

        /// <summary>
        /// Дистанция мира до точки, в которой runtime resolver определяет результат прыжка.
        /// </summary>
        public float ResolveTravel { get; }

        /// <summary>
        /// Смещение fire shift между полным action и resolver-точкой.
        /// </summary>
        public float ResolveFireShiftOffset { get; }

        /// <summary>
        /// Возвращает world-shift до runtime resolver-точки.
        /// </summary>
        public float GetResolveFireShift(float fireShift)
        {
            return fireShift + ResolveFireShiftOffset;
        }
    }

    /// <summary>
    /// Описывает runtime-различия вариантов напрыгивания на дорожный smallAlive.
    /// </summary>
    internal interface IJumpOnPolicy
    {
        /// <summary>
        /// Тип action для конкретного варианта jump-on.
        /// </summary>
        BotActionKind ActionKind { get; }

        /// <summary>
        /// Стоимость action в энергии.
        /// </summary>
        int EnergyCost { get; }

        /// <summary>
        /// Префикс человекочитаемого описания action.
        /// </summary>
        string DescriptionPrefix { get; }

        /// <summary>
        /// Тег диагностических сообщений стратегии.
        /// </summary>
        string LogTag { get; }

        /// <summary>
        /// Runtime-состояние, которое подтверждает успешное напрыгивание.
        /// </summary>
        HamsterStateEnum ExpectedJumpOnState { get; }

        /// <summary>
        /// Возвращает runtime-дистанции для текущего варианта jump-on действия.
        /// </summary>
        bool TryGetTravel(out JumpOnTravel travel);

        /// <summary>
        /// Вызывает runtime resolver для конкретного варианта прыжка.
        /// </summary>
        JumpResolveResult Resolve(
            IReadOnlyList<JumpObstacleData> obstacles,
            JumpResolveContext context);
    }
}
