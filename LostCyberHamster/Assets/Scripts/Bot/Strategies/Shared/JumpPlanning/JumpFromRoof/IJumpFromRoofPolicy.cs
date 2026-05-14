using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpFromRoof
{
    /// <summary>
    /// Описывает различия между вариантами прыжка с крыши на дорогу.
    /// </summary>
    internal interface IJumpFromRoofPolicy
    {
        BotActionKind ActionKind { get; }
        int EnergyCost { get; }
        string DescriptionPrefix { get; }
        HamsterStateEnum ExpectedSuccessState { get; }

        bool TryGetTravel(out JumpFromRoofTravel travel);

        JumpResolveResult Resolve(
            IReadOnlyList<JumpObstacleData> obstacles,
            RoofJumpResolveContext context);
    }
}
