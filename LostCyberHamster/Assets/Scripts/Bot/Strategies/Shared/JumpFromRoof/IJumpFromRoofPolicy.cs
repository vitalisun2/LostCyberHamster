using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpFromRoof
{
    /// <summary>
    /// Описывает runtime-различия вариантов прыжка с крыши на дорогу.
    /// </summary>
    internal interface IJumpFromRoofPolicy
    {
        BotActionKind ActionKind { get; }
        int EnergyCost { get; }
        string DescriptionPrefix { get; }
        HamsterStateEnum ExpectedSuccessState { get; }
        float BigAliveCollisionPaddingRatio { get; }

        /// <summary>
        /// Возвращает runtime-дистанции автоматического схода и прыжка с крыши.
        /// </summary>
        bool TryGetTravel(out JumpFromRoofTravel travel);

        /// <summary>
        /// Вызывает runtime resolver конкретного варианта roof jump.
        /// </summary>
        JumpResolveResult Resolve(
            IReadOnlyList<JumpObstacleData> obstacles,
            RoofJumpResolveContext context);
    }
}
