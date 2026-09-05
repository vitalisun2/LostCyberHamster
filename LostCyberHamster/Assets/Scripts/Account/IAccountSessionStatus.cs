using System;

namespace Assets.Scripts.Account
{
    /// <summary>Отделяет действующий access token от сохранённой личности игрока.</summary>
    public interface IAccountSessionStatus
    {
        bool IsAuthorized { get; }
        event Action SessionExpired;
    }
}
