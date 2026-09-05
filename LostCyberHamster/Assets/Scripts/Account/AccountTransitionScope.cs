using System;

namespace Assets.Scripts.Account
{
    /// <summary>Исключает новый rewarded показ во время смены SDK identity.</summary>
    public sealed class AccountTransitionScope : IDisposable
    {
        private static int _activeCount;
        private bool _disposed;
        public static bool IsActive => _activeCount > 0;
        public AccountTransitionScope() => _activeCount++;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _activeCount = Math.Max(0, _activeCount - 1);
        }
    }
}