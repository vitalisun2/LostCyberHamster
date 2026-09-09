using System;
using Assets.Scripts.System;
using Assets.Scripts.Tutorial;
using GameManagement;
using UnityEngine.SceneManagement;

namespace Vues.GameCore.ReturnActivities
{
    /// <summary>Связывает production-попытку с профилем и сценой; revive сохраняет этот объект.</summary>
    public sealed class ActivityAttemptContext
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public string Level { get; }
        private readonly string _profile;
        private readonly long _generation;
        private readonly int _scene;
        private bool _persisted;
        public bool Committed { get; internal set; }
        public static ActivityAttemptContext PendingCompletion { get; private set; }
        private const string JournalFeature = "return-activity-attempt";

        public ActivityAttemptContext(string level)
        {
            Level = level;
            _profile = GameDataManager.ProfileId;
            _generation = GameDataManager.Generation;
            _scene = SceneManager.GetActiveScene().handle;
        }

        public bool IsCurrent => !Committed && _profile == GameDataManager.ProfileId &&
            _generation == GameDataManager.Generation && _scene == SceneManager.GetActiveScene().handle &&
            GameDataManager.PlayerData?.CurrentLevel == Level && IsProductionLevel(Level);

        /// <summary>Фиксирует ID до транзакции результата, допуская повтор после ошибки записи.</summary>
        public bool Prepare()
        {
            if (!IsCurrent) return false;
            if (!_persisted)
            {
                GameDataManager.ExecuteTechnicalTransaction(() => GameDataManager.SetJournalJson(JournalFeature, Id));
                _persisted = true;
            }
            return GameDataManager.GetJournalJson(JournalFeature) == Id;
        }

        /// <summary>Передаёт контекст через синхронное событие уровня и гарантированно освобождает его.</summary>
        public void Complete(Action completion)
        {
            if (Committed) return;
            bool eligible = Prepare();
            var previous = PendingCompletion;
            try
            {
                PendingCompletion = eligible ? this : null;
                completion();
            }
            finally { PendingCompletion = previous; }
        }

        public static bool IsProductionLevel(string level) => GameDataManager.IsLoaded &&
            (SceneManager.GetActiveScene().name == "Game" || GameDataManager.IsProgressionTestingProfile) &&
            !AutomationRuntimePrefs.IsTestLevelAutomationRun() && !TutorialStorage.IsPlayerDataBackupActive &&
            !string.IsNullOrEmpty(level) && level.IndexOf("tutorial", StringComparison.OrdinalIgnoreCase) < 0 &&
            level.IndexOf("/test", StringComparison.OrdinalIgnoreCase) < 0;
    }
}
