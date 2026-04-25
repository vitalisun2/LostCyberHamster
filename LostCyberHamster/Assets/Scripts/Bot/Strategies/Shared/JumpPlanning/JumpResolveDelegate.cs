using System.Collections.Generic;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning
{
    /// <summary>
    /// Резолвит runtime-исход jump-подобного действия.
    /// </summary>
    internal delegate JumpResolveResult JumpResolveDelegate(
        IReadOnlyList<JumpObstacleData> obstacles,
        JumpResolveContext context);
}
