using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.GameEngine.Mechanics.Models
{
    class JumpResult
    {
        public HamsterStateEnum State { get; }
        public Obstacle? Target { get; }

        public JumpResult(HamsterStateEnum state, Obstacle? target)
        {
            State = state;
            Target = target;
        }
    }
}
