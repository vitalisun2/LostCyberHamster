using Assets.Scripts.Gameplay;

namespace Vues.GameCore
{
    /// <summary>
    /// Предоставляет collision owner только активный Skateboard contact contract.
    /// </summary>
    public interface ISkateboardCollisionHandler
    {
        bool IsActive { get; }

        SkateboardCollisionResult ResolveCollision(
            Obstacle obstacle,
            bool isOnBottomLine);
    }
}
