using System;
using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Planning.DecisionPointsNew
{
    /// <summary>
    /// Хранит obstacle, его индекс в world snapshot и factual-роли для planning.
    /// </summary>
    public sealed class ObstacleChainElementNew
    {
        /// <summary>
        /// Создает элемент role-based chain.
        /// </summary>
        public ObstacleChainElementNew(
            ObstacleSnapshot obstacle,
            int worldIndex,
            ObstacleRole roles)
        {
            Obstacle = obstacle ?? throw new ArgumentNullException(nameof(obstacle));

            if (worldIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(worldIndex));

            WorldIndex = worldIndex;
            Roles = roles;
        }

        public ObstacleSnapshot Obstacle { get; }
        public int WorldIndex { get; }
        public ObstacleRole Roles { get; }
        public bool IsBottomLine => Obstacle.IsBottomLine;

        /// <summary>
        /// Возвращает true, если элемент содержит все переданные role flags.
        /// </summary>
        public bool HasRole(ObstacleRole role)
        {
            return role != ObstacleRole.None
                && (Roles & role) == role;
        }

        /// <summary>
        /// Возвращает true, если элемент содержит хотя бы одну из переданных role flags.
        /// </summary>
        public bool HasAnyRole(ObstacleRole roles)
        {
            return roles != ObstacleRole.None
                && (Roles & roles) != ObstacleRole.None;
        }

        /// <summary>
        /// Возвращает true, если obstacle должен участвовать в planning chain.
        /// </summary>
        public bool HasAnyActivePlanningRole
        {
            get
            {
                ObstacleRole activeRoles = Roles & ~ObstacleRole.Collectible;
                return activeRoles != ObstacleRole.None;
            }
        }
    }
}
