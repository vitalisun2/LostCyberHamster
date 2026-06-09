using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Common.Models;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpOver
{
    /// <summary>
    /// Описывает различия между вариантами перепрыгивания препятствий на дороге.
    /// </summary>
    internal interface IJumpOverPolicy
    {
        BotActionKind ActionKind { get; }
        int EnergyCost { get; }
        string DescriptionPrefix { get; }
        HamsterStateEnum ExpectedOverState { get; }
        bool DamageBigAliveWithoutYByReach { get; }
        float BigAliveCollisionPaddingRatio { get; }

        bool CanJumpOverObstacle(ObstacleTypeEnum obstacleType);

        bool TryGetTravel(out float travel);

        JumpResolveResult Resolve(
            IReadOnlyList<JumpObstacleData> obstacles,
            JumpResolveContext context);
    }
}
