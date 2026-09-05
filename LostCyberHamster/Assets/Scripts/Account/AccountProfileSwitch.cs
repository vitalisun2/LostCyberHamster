using System;

namespace Assets.Scripts.Account
{
    [Serializable]
    public sealed class AccountProfileSwitch
    {
        public string OriginalPlayerId;
        public string OriginalProfile;
        public string CandidateProfile;
        public string CandidatePlayerId;
    }
}