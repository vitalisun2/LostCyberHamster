using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;

namespace Assets.Scripts.Bot.Planning.DecisionPoints
{
    /// <summary>
    /// Хранит obstacle, его индекс в world snapshot и factual-роли для planning.
    /// </summary>
    public sealed class ObstacleChainElement
    {
        private readonly HashSet<ObstacleRole> _roles;

        /// <summary>
        /// Создает элемент role-based chain.
        /// </summary>
        public ObstacleChainElement(
            ObstacleSnapshot obstacle,
            int worldIndex,
            IEnumerable<ObstacleRole> roles)
        {
            Obstacle = obstacle ?? throw new ArgumentNullException(nameof(obstacle));

            if (worldIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(worldIndex));

            WorldIndex = worldIndex;
            _roles = roles != null
                ? new HashSet<ObstacleRole>(roles)
                : throw new ArgumentNullException(nameof(roles));
        }

        public ObstacleSnapshot Obstacle { get; }
        public int WorldIndex { get; }
        public IReadOnlyCollection<ObstacleRole> Roles => _roles;
        public bool IsBottomLine => Obstacle.IsBottomLine;

        /// <summary>
        /// Возвращает true, если элемент содержит указанную роль.
        /// </summary>
        public bool HasRole(ObstacleRole role)
        {
            return _roles.Contains(role);
        }

        /// <summary>
        /// Возвращает true, если элемент содержит хотя бы одну из переданных ролей.
        /// </summary>
        public bool HasAnyRole(IEnumerable<ObstacleRole> roles)
        {
            if (roles == null)
                return false;

            foreach (ObstacleRole role in roles)
            {
                if (_roles.Contains(role))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Возвращает true, если obstacle должен участвовать в planning chain.
        /// </summary>
        public bool HasAnyActivePlanningRole
        {
            get
            {
                return _roles.Count > 0;
            }
        }

        /// <summary>
        /// Возвращает true, если obstacle требует обязательного planning-решения.
        /// </summary>
        public bool HasAnyRequiredPlanningRole
        {
            get
            {
                foreach (ObstacleRole role in _roles)
                {
                    if (role != ObstacleRole.Collectible)
                        return true;
                }

                return false;
            }
        }
    }
}
