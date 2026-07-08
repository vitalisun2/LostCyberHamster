using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Diagnostics;

namespace Assets.Scripts.Bot.Planning.DecisionPoints
{
    /// <summary>
    /// Хранит obstacle, его индекс в world snapshot и factual-роли для planning.
    /// </summary>
    public sealed class ObstacleChainElement
    {
        private const int RoleMaskCount = 1 << 5;
        private const ObstacleRoleMask AllRoleMask =
            ObstacleRoleMask.BlockingThreat
            | ObstacleRoleMask.RoofSupport
            | ObstacleRoleMask.Target
            | ObstacleRoleMask.RoofOccupantHazard
            | ObstacleRoleMask.Collectible;
        private static readonly IReadOnlyCollection<ObstacleRole>[] RolesByMask = CreateRolesByMask();

        private readonly ObstacleRoleMask _roleMask;

        public ObstacleChainElement(
            ObstacleSnapshot obstacle,
            int worldIndex,
            ObstacleRoleMask roleMask)
        {
            Obstacle = obstacle ?? throw new ArgumentNullException(nameof(obstacle));

            if (worldIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(worldIndex));

            if ((roleMask & ~AllRoleMask) != 0)
                throw new ArgumentOutOfRangeException(nameof(roleMask), roleMask, null);

            WorldIndex = worldIndex;
            _roleMask = roleMask;
            RuntimePerformanceDiagnostics.Count(RuntimePerformanceCounter.ObstacleChainElementConstructed);
        }

        /// <summary>
        /// Создает элемент role-based chain.
        /// </summary>
        public ObstacleChainElement(
            ObstacleSnapshot obstacle,
            int worldIndex,
            IEnumerable<ObstacleRole> roles)
            : this(obstacle, worldIndex, ToMask(roles))
        {
        }

        public ObstacleSnapshot Obstacle { get; }
        public int WorldIndex { get; }
        public IReadOnlyCollection<ObstacleRole> Roles => RolesByMask[(int)_roleMask];
        public bool IsBottomLine => Obstacle.IsBottomLine;

        /// <summary>
        /// Возвращает true, если элемент содержит указанную роль.
        /// </summary>
        public bool HasRole(ObstacleRole role)
        {
            return (_roleMask & ToMask(role)) != 0;
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
                if (HasRole(role))
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
                return _roleMask != ObstacleRoleMask.None;
            }
        }

        /// <summary>
        /// Возвращает true, если obstacle требует обязательного planning-решения.
        /// </summary>
        public bool HasAnyRequiredPlanningRole
        {
            get
            {
                return (_roleMask & ~ObstacleRoleMask.Collectible) != 0;
            }
        }

        private static ObstacleRoleMask ToMask(IEnumerable<ObstacleRole> roles)
        {
            if (roles == null)
                throw new ArgumentNullException(nameof(roles));

            ObstacleRoleMask roleMask = ObstacleRoleMask.None;
            foreach (ObstacleRole role in roles)
                roleMask |= ToMask(role);

            return roleMask;
        }

        private static ObstacleRoleMask ToMask(ObstacleRole role)
        {
            switch (role)
            {
                case ObstacleRole.BlockingThreat:
                    return ObstacleRoleMask.BlockingThreat;
                case ObstacleRole.RoofSupport:
                    return ObstacleRoleMask.RoofSupport;
                case ObstacleRole.Target:
                    return ObstacleRoleMask.Target;
                case ObstacleRole.RoofOccupantHazard:
                    return ObstacleRoleMask.RoofOccupantHazard;
                case ObstacleRole.Collectible:
                    return ObstacleRoleMask.Collectible;
                default:
                    throw new ArgumentOutOfRangeException(nameof(role), role, null);
            }
        }

        private static IReadOnlyCollection<ObstacleRole>[] CreateRolesByMask()
        {
            var rolesByMask = new IReadOnlyCollection<ObstacleRole>[RoleMaskCount];
            for (int mask = 0; mask < RoleMaskCount; mask++)
            {
                var roles = new List<ObstacleRole>(5);
                AddRoleIfMaskHas(roles, mask, ObstacleRoleMask.BlockingThreat, ObstacleRole.BlockingThreat);
                AddRoleIfMaskHas(roles, mask, ObstacleRoleMask.RoofSupport, ObstacleRole.RoofSupport);
                AddRoleIfMaskHas(roles, mask, ObstacleRoleMask.Target, ObstacleRole.Target);
                AddRoleIfMaskHas(roles, mask, ObstacleRoleMask.RoofOccupantHazard, ObstacleRole.RoofOccupantHazard);
                AddRoleIfMaskHas(roles, mask, ObstacleRoleMask.Collectible, ObstacleRole.Collectible);
                rolesByMask[mask] = roles.ToArray();
            }

            return rolesByMask;
        }

        private static void AddRoleIfMaskHas(
            List<ObstacleRole> roles,
            int mask,
            ObstacleRoleMask roleMask,
            ObstacleRole role)
        {
            if ((mask & (int)roleMask) != 0)
                roles.Add(role);
        }
    }
}
