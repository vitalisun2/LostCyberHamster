using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.GameEngine.Mechanics.Models
{
    public readonly struct JumpResolveResult
    {
        public readonly HamsterStateEnum State;
        public readonly int TargetIndex;

        public JumpResolveResult(HamsterStateEnum state, int targetIndex)
        {
            State = state;
            TargetIndex = targetIndex;
        }
    }
}
