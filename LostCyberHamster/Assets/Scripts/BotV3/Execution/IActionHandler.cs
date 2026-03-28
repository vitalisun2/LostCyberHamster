using Assets.Scripts.Gameplay;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Обработчик конкретного действия бота (Jump, SwitchLane).
    /// Отвечает за отправку команды хомяку и определение момента завершения.
    /// </summary>
    internal interface IActionHandler
    {
        void Fire(Hamster hamster, BranchStep step);
        bool IsCompleted(Hamster hamster, BranchStep step);

        /// <summary>
        /// Дополнительная проверка перед fire. Возвращает true, если действие нужно отложить.
        /// </summary>
        bool ShouldDelay(Hamster hamster, BranchStep step);
    }
}
