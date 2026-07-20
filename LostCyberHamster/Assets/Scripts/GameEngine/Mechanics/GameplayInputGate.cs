using System;
using System.Collections.Generic;

namespace Assets.Scripts.GameEngine.Mechanics
{
    /// <summary>
    /// Общая точка временной блокировки внешнего gameplay-ввода.
    /// Поддерживает несколько независимых владельцев блокировки.
    /// </summary>
    public static class GameplayInputGate
    {
        private static readonly HashSet<object> _owners = new();

        public static bool IsBlocked => _owners.Count > 0;

        public static void SetBlocked(object owner, bool isBlocked)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (isBlocked)
            {
                _owners.Add(owner);
                return;
            }

            _owners.Remove(owner);
        }
    }
}
