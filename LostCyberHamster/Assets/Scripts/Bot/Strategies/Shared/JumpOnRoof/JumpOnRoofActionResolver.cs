using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning.DecisionPoints;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpOnRoof
{
    /// <summary>
    /// Выбирает roof support из role-based chain для jump-on-roof действия.
    /// </summary>
    internal sealed class JumpOnRoofActionResolver
    {
        /// <summary>
        /// Возвращает первый roof support текущей role-based ситуации.
        /// </summary>
        public bool TryResolve(
            ObstacleChain chain,
            out ObstacleSnapshot targetRoof,
            out int targetRoofIndex,
            out int targetRoofChainIndex)
        {
            targetRoof = null;
            targetRoofIndex = -1;
            targetRoofChainIndex = -1;

            if (chain == null)
                return false;

            if (!chain.TryFindFirstWithRole(
                    ObstacleRole.RoofSupport,
                    out ObstacleChainElement roofElement,
                    out targetRoofChainIndex))
            {
                return false;
            }

            targetRoof = roofElement.Obstacle;
            targetRoofIndex = roofElement.WorldIndex;
            return true;
        }
    }
}
