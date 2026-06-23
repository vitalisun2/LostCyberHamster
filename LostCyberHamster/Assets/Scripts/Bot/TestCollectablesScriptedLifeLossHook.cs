using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Diagnostics;
using Assets.Scripts.Gameplay;
using Assets.Scripts.GameEngine;
using GameManagement;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Изолирует редакторский тестовый сценарий test_collectables, где перед каждым life collectible
    /// нужен свободный life slot без запуска игровой механики damage-state.
    /// </summary>
    internal sealed class TestCollectablesScriptedLifeLossHook
    {
#if UNITY_EDITOR
        /// <summary>
        /// Адрес test level, где проверяется приоритизация collectables.
        /// </summary>
        private const string _testCollectablesLevelAddress = "01_New_York/Morning/test_collectables";

        /// <summary>
        /// Patterns с life collectible, перед которыми test level должен иметь недостающую жизнь.
        /// </summary>
        private static readonly HashSet<string> _testCollectablesLifePatternNames = new HashSet<string>
        {
            "test_collectables_04",
            "test_collectables_05"
        };

        private readonly HashSet<string> _scriptedLifeLossAppliedPatternNames = new HashSet<string>();
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
            _scriptedLifeLossAppliedPatternNames.Clear();
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
        /// Перед оценкой pattern-а с life collectible создает свободный life slot.
        /// Это заменяет физический damage pattern, который мог дать каскадные столкновения и остановить проверку раньше.
        /// </summary>
        public void TryApplyBeforePatternEvaluation(int patternIndex, string patternName)
        {
#if UNITY_EDITOR
            // Проверяет, что сценарий применим ровно к нужному тестовому pattern-у.
            if (_scriptedLifeLossAppliedPatternNames.Contains(patternName)
                || !IsCurrentTestCollectablesLevel()
                || !_testCollectablesLifePatternNames.Contains(patternName))
            {
                return;
            }

            _scriptedLifeLossAppliedPatternNames.Add(patternName);
            TryApplyLifeLoss(patternIndex, patternName);
#endif
        }

#if UNITY_EDITOR
        private void TryApplyLifeLoss(int patternIndex, string patternName)
        {
            Hamster hamster = _hamsterProvider();
            if (hamster == null)
                return;

            // Каждый life-сценарий должен иметь собственный свободный life slot.
            // Если предыдущий life pickup еще не исполнен, второй slot создается заранее.
            if (hamster.Lives.Value <= 1)
            {
                DebugManager.DiagLog(
                    $"[Bot TEST] scripted life loss skipped reason=minimum_lives_guard " +
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
        }

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
