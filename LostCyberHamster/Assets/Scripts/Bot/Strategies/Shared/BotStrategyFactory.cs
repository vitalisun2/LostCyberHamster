using System.Collections.Generic;
using Assets.Scripts.Bot.Strategies.JumpOnRoof;
using Assets.Scripts.Bot.Strategies.JumpOver;
using Assets.Scripts.Bot.Strategies.SuperJumpOver;
using Assets.Scripts.Bot.Strategies.SwitchLane;
using Assets.Scripts.Bot.Strategies.Shared.Interfaces;
using Assets.Scripts.Bot.Strategies.Shared.Models;

namespace Assets.Scripts.Bot.Strategies.Shared
{
    /// <summary>
    /// Создаёт полный набор strategy-компонентов бота.
    /// </summary>
    internal static class BotStrategyFactory
    {
        public static IReadOnlyList<IPlanningStrategy> CreateAll()
        {
            return new IPlanningStrategy[]
            {
                new SwitchLaneStrategy(),
                new JumpOverStrategy(),
                new SuperJumpOverStrategy(),
                new JumpOnRoofStrategy()
            };
        }
    }
}
