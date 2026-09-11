using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.GameEngine.Mechanics.Models
{
    public readonly struct JumpResult
    {
        public readonly HamsterStateEnum State;
        public readonly Obstacle? Target;
        public readonly Obstacle? DamageSource;

        public JumpResult(HamsterStateEnum state, Obstacle? target, Obstacle? damageSource = null)
        {
            State = state;
            Target = target;
            DamageSource = damageSource;
        }
    }
}
