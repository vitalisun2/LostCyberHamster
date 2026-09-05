using System;
using UnityEngine;

namespace GameAds
{
    /// <summary>Контекст одного показа; завершение UI не уничтожает SDK listener.</summary>
    public sealed class RewardedAdRequest
    {
        internal RewardedAdIntent Intent;
        internal double StartedAt;
        internal double SubmittedAt;
        internal double ForegroundSeconds;
        internal double RetrySettlementAt;
        internal IDisposable ProfileBlock;
        internal Action Revive;
        internal Func<bool> CanRevive;
        internal bool ContextCancelled;
        internal bool CompletionAccepted;
        internal bool TerminalReceived;
        internal bool LoadCompleted;
        public RewardedAdState State { get; internal set; }
        public string RequestId => Intent.RequestId;
        public bool IsFinished => State == RewardedAdState.Completed ||
            State == RewardedAdState.Failed || State == RewardedAdState.Skipped ||
            State == RewardedAdState.Cancelled;
        public bool IsNativePending => State == RewardedAdState.ShowSubmitted ||
            State == RewardedAdState.Showing || State == RewardedAdState.AwaitingResult;
        public event Action<RewardedAdRequest> Changed;

        internal void Notify()
        {
            var handlers = Changed;
            if (handlers == null)
                return;
            foreach (Action<RewardedAdRequest> handler in handlers.GetInvocationList())
            {
                try { handler(this); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }
    }
}
