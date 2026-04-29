using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.GameEngine.Mechanics.Models;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.Policies
{
    /// <summary>
    /// Проверяет, может ли хомяк безопасно дождаться fire shift до старта jump-действия.
    /// </summary>
    internal interface IPreFireSafetyPolicy
    {
        bool CanWaitUntilFire(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> obstacles,
            float fireShift);
    }
}