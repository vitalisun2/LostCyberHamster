namespace Assets.Scripts.Bot.Strategies.Shared.Simulation
{
    /// <summary>
    /// Описывает, как in-progress projection должен выбрать начальную точку повторного obstacle scan
    /// после уже запущенного head-action.
    /// </summary>
    /// <remarks>
    /// Этот value object существует, чтобы simulator явно называл доменный смысл своего action target.
    /// Generic projection helper не должен сам угадывать, означает ли target "уже обработанное препятствие",
    /// "границу ожидания", "подобранный collectable" или просто timing anchor. Неправильное неявное
    /// предположение приводит к пропуску ещё актуальных препятствий при async tail planning.
    ///
    /// Выбор factory-метода должен делаться в конкретном simulator-е, потому что именно simulator владеет
    /// контрактом action-а: что считается решённым после completion, нужно ли пересканировать другую линию,
    /// и какой obstacle должен быть исключён из projected snapshot.
    /// </remarks>
    internal readonly struct InProgressProjectionOptions
    {
        private InProgressProjectionOptions(
            bool skipResolvedActionTarget,
            int? startObstacleIndexOverride,
            int? removedObstacleInstanceIdAfterCompletion)
        {
            ShouldSkipResolvedActionTarget = skipResolvedActionTarget;
            StartObstacleIndexOverride = startObstacleIndexOverride;
            RemovedObstacleInstanceIdAfterCompletion = removedObstacleInstanceIdAfterCompletion;
        }

        /// <summary>
        /// Возвращает true, когда action target после completion считается уже решённым planning decision
        /// и scan можно начинать не раньше следующего obstacle index.
        /// </summary>
        public bool ShouldSkipResolvedActionTarget { get; }

        /// <summary>
        /// Явная позиция, с которой нужно начинать повторный obstacle scan.
        /// </summary>
        public int? StartObstacleIndexOverride { get; }

        /// <summary>
        /// Instance id obstacle, который должен считаться отсутствующим в projected planning state.
        /// </summary>
        public int? RemovedObstacleInstanceIdAfterCompletion { get; }

        /// <summary>
        /// Используется для action-ов, которые только продвигают время или меняют состояние хомяка,
        /// но не гарантируют, что target obstacle можно пропустить.
        /// </summary>
        /// <remarks>
        /// Пример: `PassiveAdvance`, где target является opposite-lane boundary для ожидания, а не
        /// препятствием, которое action обработал; `PassiveRoofExit`, где после перехода в Run нужно
        /// продолжить анализ с текущего индекса; ground/roof jump-over варианты, где projection shift
        /// сам отфильтрует препятствия, уже оказавшиеся позади хомяка.
        /// </remarks>
        public static InProgressProjectionOptions KeepCurrentObstacleScan()
        {
            return new InProgressProjectionOptions(
                skipResolvedActionTarget: false,
                startObstacleIndexOverride: null,
                removedObstacleInstanceIdAfterCompletion: null);
        }

        /// <summary>
        /// Используется для action-ов, где target после completion является решённой planning-точкой
        /// и его не нужно повторно рассматривать как следующий obstacle.
        /// </summary>
        /// <remarks>
        /// Пример: landing/roof-to-roof действия, где target roof или roof obstacle уже является
        /// достигнутой опорой/пройденным объектом, но сам объект не удаляется из мира.
        /// </remarks>
        public static InProgressProjectionOptions SkipResolvedActionTarget()
        {
            return new InProgressProjectionOptions(
                skipResolvedActionTarget: true,
                startObstacleIndexOverride: null,
                removedObstacleInstanceIdAfterCompletion: null);
        }

        /// <summary>
        /// Используется, когда после action-а нужно пересобрать next obstacle с начала snapshot-а,
        /// потому что прежний `NextObstacleIndex` больше не надёжен.
        /// </summary>
        /// <remarks>
        /// Пример: смена линии без collectable pickup. После lane switch препятствия, которые раньше
        /// были на другой линии и находились до текущего индекса, могут стать релевантными для нового
        /// состояния хомяка.
        /// </remarks>
        public static InProgressProjectionOptions RescanFromStart()
        {
            return new InProgressProjectionOptions(
                skipResolvedActionTarget: false,
                startObstacleIndexOverride: 0,
                removedObstacleInstanceIdAfterCompletion: null);
        }

        /// <summary>
        /// Используется, когда action подбирает или уничтожает конкретный obstacle, после чего нужно
        /// пересканировать snapshot с начала, игнорируя этот removed instance id.
        /// </summary>
        /// <remarks>
        /// Пример: `PassiveCollect`, `JumpOn` и roof-to-road jump-on варианты. Rescan from start нужен,
        /// чтобы не потерять препятствия, которые после смены состояния или удаления target-а снова
        /// становятся ближайшими релевантными объектами.
        /// </remarks>
        public static InProgressProjectionOptions RemoveObstacleAndRescan(int obstacleInstanceId)
        {
            return new InProgressProjectionOptions(
                skipResolvedActionTarget: false,
                startObstacleIndexOverride: 0,
                removedObstacleInstanceIdAfterCompletion: obstacleInstanceId);
        }
    }
}
