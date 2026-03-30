using Assets.Scripts.Gameplay;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Обработчик конкретного действия бота (Jump, SwitchLane).
    /// Отвечает за отправку команды хомяку и определение момента завершения.
    /// </summary>
    internal interface IActionHandler
    {
        void Fire(Hamster hamster, BranchStep step);
        bool IsCompleted(Hamster hamster, BranchStep step);
    }

    /// <summary>
    /// Опциональная runtime-проверка перед Fire.
    /// Нужна только для действий, где planner задаёт канонический момент, а runtime может безопасно подождать ещё кадр.
    /// </summary>
    internal interface IPreFireDelayHandler
    {
        bool ShouldDelay(Hamster hamster, BranchStep step);
    }
}
