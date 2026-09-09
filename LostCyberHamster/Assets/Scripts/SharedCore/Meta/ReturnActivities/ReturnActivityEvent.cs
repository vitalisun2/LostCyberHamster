using System;

namespace Vues.GameCore.ReturnActivities
{
    /// <summary>Квитанция бизнес-события для отправки с устойчивым идентификатором.</summary>
    [Serializable]
    public sealed class ReturnActivityEvent
    {
        public string Id;
        public string Action;
        public string Kind;
        public string Period;
        public string Correlation;
        public int Step;
        public int Wins;
        public int Days;
        public int Coins;
        public int Gems;
    }
}
