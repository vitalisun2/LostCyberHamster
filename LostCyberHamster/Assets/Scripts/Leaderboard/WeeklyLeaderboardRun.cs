using System;

namespace GameManagement.Leaderboard
{
    /// <summary>Хранит неизменяемый адрес забега и durable-состояние отправки.</summary>
    [Serializable]
    public sealed class WeeklyLeaderboardRun
    {
        public string RunId;
        public string OwnerPlayerId;
        public string ProfileId;
        public string Environment;
        public string LeaderboardId;
        public string VersionId;
        public int Score;
        public bool SendAttempted;
        public bool HadPreviousEntry;
        public int PreviousBest;
        public int WeeklyBest;
        public WeeklyRunStatus Status;
    }
}
