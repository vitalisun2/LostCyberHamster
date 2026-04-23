using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public static class TapOutcomeResolver
    {
        /// <summary>
        /// Проверяет, примет ли runtime tap перед сменой линии.
        /// </summary>
        public static bool CanAcceptTap(
            HamsterStateEnum hamsterState,
            bool isShifting)
        {
            // Проверяем, можно ли сейчас начать смену линии.
            if (isShifting)
                return false;

            if (hamsterState != HamsterStateEnum.Run
                && hamsterState != HamsterStateEnum.RoofRun)
            {
                return false;
            }

            return true;
        }
    }
}
