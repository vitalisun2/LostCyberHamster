using System;
using System.Collections.Generic;
using Assets.Scripts.GameEngine.Mechanics;

namespace LostCyberHamster.UI
{
    /// <summary>Независимый токен блокировки UI и непосредственного игрового ввода.</summary>
    public sealed class UiInputBlock : IDisposable
    {
        private static readonly HashSet<UiInputBlock> Owners = new();

        public static bool IsBlocked => Owners.Count > 0;

        private UiInputBlock()
        {
            Owners.Add(this);
            GameplayInputGate.SetBlocked(this, true);
        }

        public static IDisposable Acquire() => new UiInputBlock();

        public void Dispose()
        {
            if (Owners.Remove(this))
                GameplayInputGate.SetBlocked(this, false);
        }
    }
}
