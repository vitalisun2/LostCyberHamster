using System;

namespace Vues.GameCore.ReturnActivities
{
    /// <summary>Сохраняет идентичность последней квалифицирующей победы вместе с её игровым периодом.</summary>
    [Serializable]
    public sealed class ActivityWinReceipt
    {
        public string AttemptId;
        public string Level;
        public int Stars;
        public string CommittedUtc;
        public string Day;
        public string Week;
    }
}
