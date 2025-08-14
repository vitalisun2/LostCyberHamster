namespace Assets.Scripts.GameManagerLogic
{
    public enum GameState
    {
        /// <summary>
        /// Игра неактивна, начальное состояние
        /// </summary>
        OFF = 0,

        /// <summary>
        /// Фаза интро до игрового процесса
        /// </summary>
        INTRO = 1,

        /// <summary>
        /// Игра идет, игрок активно играет
        /// </summary>
        PLAYING = 2,

        /// <summary>
        /// Игра приостановлена, управление заморожено
        /// </summary>
        PAUSED = 3,

        /// <summary>
        /// Игра завершена, показ результатов
        /// </summary>
        FINISHED = 4
    }


}
