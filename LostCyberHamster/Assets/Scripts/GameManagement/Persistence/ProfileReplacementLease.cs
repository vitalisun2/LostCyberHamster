using System;

namespace GameManagement
{
    /// <summary>Удерживает профиль до завершения операции, привязанной к его данным.</summary>
    internal sealed class ProfileReplacementLease : IDisposable
    {
        private Action _release;

        public ProfileReplacementLease(Action release) { _release = release; }

        public void Dispose()
        {
            var release = _release;
            _release = null;
            release?.Invoke();
        }
    }
}
