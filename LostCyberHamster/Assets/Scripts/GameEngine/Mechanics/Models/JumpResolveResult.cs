using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.GameEngine.Mechanics.Models
{
    public readonly struct JumpResolveResult
    {
        public readonly HamsterStateEnum State;
        public readonly int TargetIndex;
        public readonly int DamageSourceIndex;

        public JumpResolveResult(HamsterStateEnum state, int targetIndex, int damageSourceIndex = -1)
        {
            State = state;
            TargetIndex = targetIndex;
            DamageSourceIndex = damageSourceIndex;
        }
    }
}
