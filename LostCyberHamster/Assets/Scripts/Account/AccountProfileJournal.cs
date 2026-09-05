using System;
using System.Collections.Generic;

namespace Assets.Scripts.Account
{
    /// <summary>Сопоставляет подтверждённого игрока и изолированное хранилище SDK credentials.</summary>
    [Serializable]
    public sealed class AccountProfileJournal
    {
        public List<AccountProfileBinding> Bindings = new();
        public string LastConfirmedPlayerId;
        public AccountProfileSwitch Pending;
    }
}