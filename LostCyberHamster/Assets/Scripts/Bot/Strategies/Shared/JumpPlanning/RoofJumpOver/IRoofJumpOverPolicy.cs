using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.RoofJumpOver
{
    /// <summary>
    /// Описывает различия между вариантами перепрыгивания препятствий во время RoofRun.
    /// </summary>
    internal interface IRoofJumpOverPolicy
    {
        BotActionKind ActionKind { get; }
        int EnergyCost { get; }
        string DescriptionPrefix { get; }
        HamsterStateEnum ExpectedSuccessState { get; }

        bool TryGetTravel(out RoofJumpOverTravel travel);

        JumpResolveResult Resolve(
            IReadOnlyList<JumpObstacleData> obstacles,
            RoofJumpResolveContext context);
    }
}