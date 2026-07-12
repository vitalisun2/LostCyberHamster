#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Assets.Scripts.DevTools.Account
{
    /// <summary>
    /// Передаёт view неизменяемое presentation-состояние account DEV-инструментов.
    /// </summary>
    internal readonly struct AccountDevToolsViewState
    {
        public AccountDevToolsViewState(
            string humanStatus,
            string diagnostics,
            string lastResult,
            bool isBusy,
            bool isLinked,
            bool isLocallyReady)
        {
            HumanStatus = humanStatus ?? string.Empty;
            Diagnostics = diagnostics ?? string.Empty;
            LastResult = lastResult ?? string.Empty;
            IsBusy = isBusy;
            IsLinked = isLinked;
            IsLocallyReady = isLocallyReady;
        }

        public string HumanStatus { get; }
        public string Diagnostics { get; }
        public string LastResult { get; }
        public bool IsBusy { get; }
        public bool IsLinked { get; }
        public bool IsLocallyReady { get; }
        public bool HasResult => IsBusy || !string.IsNullOrWhiteSpace(LastResult);
    }
}
#endif
