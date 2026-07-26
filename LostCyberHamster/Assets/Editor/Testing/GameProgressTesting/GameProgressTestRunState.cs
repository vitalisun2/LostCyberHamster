using System;
using System.Collections.Generic;

namespace LostCyberHamster.Editor.Testing.GameProgress
{
    /// <summary>Сериализуемое состояние ручного прогона игрового прогресса.</summary>
    [Serializable]
    internal sealed class GameProgressTestRunState
    {
        public const int CurrentVersion = 1;

        public enum FlowStage
        {
            MainMenu,
            SelectDayPart,
            SelectLevel,
            Intro,
            Gameplay,
            Win,
            Completed,
            Cancelled
        }

        public enum CheckpointKind
        {
            None,
            Intro,
            Gameplay,
            Win
        }

        public int Version = CurrentVersion;
        public bool IsActive;
        public bool IsBusy;
        public FlowStage Stage = FlowStage.MainMenu;
        public string TargetLevelAddress = string.Empty;
        public CheckpointKind Checkpoint;
        public string CheckpointLevelAddress = string.Empty;
        public int CheckpointScore;
        public string Status = "Откройте Bootstrap в Play Mode и главный экран меню.";
        public List<string> Log = new();
    }
}
