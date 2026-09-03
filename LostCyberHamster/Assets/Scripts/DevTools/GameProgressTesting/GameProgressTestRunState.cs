#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace Assets.Scripts.DevTools.GameProgressTesting
{
    /// <summary>Хранит только навигационный контекст ручного теста поверх реального прогресса игрока.</summary>
    [Serializable]
    internal sealed class GameProgressTestRunState
    {
        public const int CurrentVersion = 3;

        public int Version = CurrentVersion;
        public bool IsActive;
        public bool IsBusy;
        public string TargetLevelAddress = string.Empty;
        public string LastCompletedLevelAddress = string.Empty;
        public string PendingCompletionLevelAddress = string.Empty;
        public int LastWinScore;
        public string Status = "Запустите игру через Bootstrap.";
        public string CurrentAction = "Действия ещё не выполнялись.";
    }
}
#endif
