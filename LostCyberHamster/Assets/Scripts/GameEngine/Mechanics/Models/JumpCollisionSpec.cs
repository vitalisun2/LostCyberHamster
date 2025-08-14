using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.GameEngine.Mechanics.Models
{
    class JumpCollisionSpec
    {
        public float EdgeTol { get; }
        public HamsterStateEnum HitState { get; }

        public JumpCollisionSpec(float edgeTol, HamsterStateEnum hitState)
        {
            EdgeTol = edgeTol;
            HitState = hitState;
        }
    }
}
