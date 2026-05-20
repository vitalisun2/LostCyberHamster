using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpFromRoofOnRoof
{
    /// <summary>
    /// Описывает runtime-отличия конкретного варианта прыжка с крыши на следующую крышу.
    /// </summary>
    internal interface IJumpFromRoofOnRoofPolicy
    {
        BotActionKind ActionKind { get; }
        int EnergyCost { get; }
        string DescriptionPrefix { get; }
        HamsterStateEnum ExpectedSuccessState { get; }
        float BigAliveCollisionPaddingRatio { get; }

        bool TryGetTravel(out JumpFromRoofOnRoofTravel travel);

        JumpResolveResult Resolve(
            IReadOnlyList<JumpObstacleData> obstacles,
            RoofJumpResolveContext context);
    }
}
