namespace Assets.Scripts.Account
{
    /// <summary>Даёт доступ к публичным SDK profiles без чтения или копирования токенов.</summary>
    public interface IAccountProfileGateway
    {
        string Profile { get; }
        void SwitchProfile(string profile);
    }
}