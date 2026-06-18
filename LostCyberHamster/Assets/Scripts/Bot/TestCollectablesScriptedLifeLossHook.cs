using System;
using Assets.Scripts.Bot.Diagnostics;
using Assets.Scripts.Gameplay;
using GameManagement;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Изолирует редакторский тестовый сценарий test_collectables, где перед life collectible
    /// нужна ровно одна недостающая жизнь без запуска игровой механики damage-state.
    /// </summary>
    internal sealed class TestCollectablesScriptedLifeLossHook
    {
#if UNITY_EDITOR
        /// <summary>
        /// Адрес test level, где проверяется приоритизация collectables.
        /// </summary>
        private const string _testCollectablesLevelAddress = "01_New_York/Morning/test_collectables";

        /// <summary>
        /// Pattern с life collectible, перед которым test level должен иметь недостающую жизнь.
        /// </summary>
        private const string _testCollectablesLifePatternName = "test_collectables_04";

        /// <summary>
        /// Максимальное число жизней, от которого тестовый сценарий создает ровно одну недостающую жизнь.
        /// </summary>
        private const int _testCollectablesMaxLives = 3;

        private bool _isScriptedLifeLossApplied;
        private bool _suppressNextScriptedLifeLossDeadEndReport;
#endif

        private readonly Func<Hamster> _hamsterProvider;
        private readonly Action _clearPendingDeadEndReport;

        /// <summary>
        /// Создает обработчик с доступом к runtime-хомяку и очистке pending dead-end диагностики.
        /// </summary>
        public TestCollectablesScriptedLifeLossHook(
            Func<Hamster> hamsterProvider,
            Action clearPendingDeadEndReport)
        {
            _hamsterProvider = hamsterProvider ?? throw new ArgumentNullException(nameof(hamsterProvider));
            _clearPendingDeadEndReport = clearPendingDeadEndReport
                ?? throw new ArgumentNullException(nameof(clearPendingDeadEndReport));
        }

        /// <summary>
        /// Сбрасывает состояние одноразового тестового сценария для нового запуска gameplay runtime.
        /// </summary>
        public void Reset()
        {
#if UNITY_EDITOR
            _isScriptedLifeLossApplied = false;
            _suppressNextScriptedLifeLossDeadEndReport = false;
#endif
        }

        /// <summary>
        /// Поглощает единственное событие потери жизни, которое этот тестовый сценарий создает в test_collectables.
        /// Так проверочный запуск не завершается как dead-end до проверки life collectible.
        /// </summary>
        public bool TryConsumeLivesLost(int livesLost)
        {
#if UNITY_EDITOR
            // Проверяет, что событие создано именно этим тестовым сценарием.
            if (!_suppressNextScriptedLifeLossDeadEndReport)
                return false;

            // Очищает pending diagnosis, чтобы scripted loss не стал подтвержденным dead-end.
            _suppressNextScriptedLifeLossDeadEndReport = false;
            _clearPendingDeadEndReport();
            Hamster hamster = _hamsterProvider();
            DebugManager.DiagLog(
                $"[Bot TEST] scripted life loss accepted livesLost={livesLost} " +
                $"lives={(hamster != null ? hamster.Lives.Value : -1)}");
            return true;
#else
            return false;
#endif
        }

        /// <summary>
        /// Перед оценкой pattern-а с life collectible создает недостающую жизнь без DamageEvent.
        /// Это заменяет физический damage pattern, который мог дать каскадные столкновения и остановить проверку раньше.
        /// </summary>
        public void TryApplyBeforePatternEvaluation(int patternIndex, string patternName)
        {
#if UNITY_EDITOR
            // Проверяет, что сценарий применим ровно к нужному тестовому pattern-у.
            if (_isScriptedLifeLossApplied
                || !IsCurrentTestCollectablesLevel()
                || !string.Equals(patternName, _testCollectablesLifePatternName, StringComparison.Ordinal))
            {
                return;
            }

            Hamster hamster = _hamsterProvider();
            if (hamster == null)
                return;

            _isScriptedLifeLossApplied = true;

            // Не создает дополнительный урон, если недостающая жизнь уже появилась раньше.
            if (hamster.Lives.Value < _testCollectablesMaxLives)
            {
                DebugManager.DiagLog(
                    $"[Bot TEST] scripted life loss skipped reason=already_missing_life " +
                    $"level={GetCurrentLevelAddressForLog()} patternIndex={patternIndex} " +
                    $"pattern={patternName} lives={hamster.Lives.Value}");
                return;
            }

            // Создает недостающую жизнь без DamageEvent, чтобы не включать состояние damage перед collectable.
            _suppressNextScriptedLifeLossDeadEndReport = true;
            hamster.Lives.Value -= 1;
            GameEventsManager.LivesLost(1);
            DebugManager.DiagLog(
                $"[Bot TEST] scripted life loss level={GetCurrentLevelAddressForLog()} " +
                $"patternIndex={patternIndex} pattern={patternName} livesLost=1 lives={hamster.Lives.Value}");
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// Проверяет, что активный уровень — именно редакторский test_collectables.
        /// </summary>
        private static bool IsCurrentTestCollectablesLevel()
        {
            return string.Equals(
                GameDataManager.PlayerData?.CurrentLevel,
                _testCollectablesLevelAddress,
                StringComparison.Ordinal);
        }

        /// <summary>
        /// Возвращает адрес текущего уровня для диагностического лога тестового сценария.
        /// </summary>
        private static string GetCurrentLevelAddressForLog()
        {
            return GameDataManager.PlayerData?.CurrentLevel ?? "<null>";
        }
#endif
    }
}
