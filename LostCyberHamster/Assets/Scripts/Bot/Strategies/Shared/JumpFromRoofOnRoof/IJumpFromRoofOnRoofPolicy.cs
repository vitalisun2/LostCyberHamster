using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpFromRoofOnRoof
{
    /// <summary>
    /// Описывает runtime-различия вариантов прыжка с текущей крыши на следующую крышу.
    /// </summary>
    internal interface IJumpFromRoofOnRoofPolicy
    {
        /// <summary>
        /// Тип planned action, который создает стратегия с этой policy.
        /// </summary>
        BotActionKind ActionKind { get; }

        /// <summary>
        /// Стоимость action в энергии хомяка.
        /// </summary>
        int EnergyCost { get; }

        /// <summary>
        /// Префикс описания planned action для диагностик.
        /// </summary>
        string DescriptionPrefix { get; }

        /// <summary>
        /// Runtime-состояние, которое resolver должен вернуть для успешной посадки на следующую крышу.
        /// </summary>
        HamsterStateEnum ExpectedSuccessState { get; }

        /// <summary>
        /// Доля ширины хомяка для дополнительного отступа перед bigAlive.
        /// </summary>
        float BigAliveCollisionPaddingRatio { get; }

        /// <summary>
        /// Возвращает runtime-дистанции roof-to-roof action.
        /// </summary>
        bool TryGetTravel(out JumpFromRoofOnRoofTravel travel);

        /// <summary>
        /// Вызывает runtime resolver конкретного варианта roof jump.
        /// </summary>
        JumpResolveResult Resolve(
            IReadOnlyList<JumpObstacleData> obstacles,
            RoofJumpResolveContext context);
    }
}
