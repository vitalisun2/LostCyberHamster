using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Обработчик действия SuperJump: отправляет SuperJumpRequest и ждёт приземления.
    /// Применяется только из состояния Run (дорога). Крышный SuperRoofJump — отдельная задача.
    /// </summary>
    internal class SuperJumpHandler : IActionHandler
    {
        public void Fire(Hamster hamster, BranchStep step)
        {
            hamster.SuperJumpRequest.Invoke();
        }

        public bool IsCompleted(Hamster hamster, BranchStep step)
        {
            return !IsActiveSuperJumpState(hamster.HamsterState.Value);
        }

        private static bool IsActiveSuperJumpState(HamsterStateEnum state)
        {
            switch (state)
            {
                case HamsterStateEnum.SuperJump:
                case HamsterStateEnum.SuperJumpOver:
                case HamsterStateEnum.SuperJumpOnObstacle:
                case HamsterStateEnum.SuperJumpOnRoof:
                case HamsterStateEnum.SuperJumpDamage:
                case HamsterStateEnum.SuperJumpOnRoofDamage:
                    return true;
                default:
                    return false;
            }
        }
    }
}
