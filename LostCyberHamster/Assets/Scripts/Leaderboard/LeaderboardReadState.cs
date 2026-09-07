using System.Threading.Tasks;

namespace GameManagement.Leaderboard
{
    /// <summary>Объединяет кеш и единственный активный запрос одной таблицы.</summary>
    internal sealed class LeaderboardReadState
    {
        public string LocationId;
        public string PartId;
        public string BoardId;
        public LeaderboardResultsSnapshot Table;
        public Task Request;
        public LeaderboardReadStatus Status = LeaderboardReadStatus.Connecting;
        public LeaderboardPersonalStatus PersonalStatus = LeaderboardPersonalStatus.Unavailable;
        public int ContentVersion;
        public bool PersonalVisible;
        public bool VerifiedThisSession;
        public double LastAttempt = double.NegativeInfinity;
        public bool RefreshAfterRequest;
    }
}
