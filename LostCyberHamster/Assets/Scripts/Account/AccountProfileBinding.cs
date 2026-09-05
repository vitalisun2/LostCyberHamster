using System;

namespace Assets.Scripts.Account
{
    [Serializable]
    public sealed class AccountProfileBinding
    {
        public string PlayerId;
        public string Profile;
        public bool IsLinked;
    }
}
