#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Threading.Tasks;

namespace Assets.Scripts.DevTools.Account
{
    /// <summary>
    /// Хранит одно ожидающее подтверждения destructive account-действие.
    /// </summary>
    internal sealed class AccountDevToolsConfirmationState
    {
        private Func<Task> _confirmedAction;

        public bool HasPending => _confirmedAction != null;
        public string Warning { get; private set; } = string.Empty;

        public void Request(string warning, Func<Task> confirmedAction)
        {
            if (confirmedAction == null)
            {
                throw new ArgumentNullException(nameof(confirmedAction));
            }

            Warning = warning ?? string.Empty;
            _confirmedAction = confirmedAction;
        }

        public bool TryConsume(out Func<Task> confirmedAction)
        {
            confirmedAction = _confirmedAction;
            Cancel();
            return confirmedAction != null;
        }

        public void Cancel()
        {
            Warning = string.Empty;
            _confirmedAction = null;
        }
    }
}
#endif
