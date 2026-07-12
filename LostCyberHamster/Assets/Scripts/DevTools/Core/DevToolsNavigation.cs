#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;

namespace Assets.Scripts.DevTools.Core
{
    /// <summary>
    /// Управляет типизированным back stack feature-экранов без зависимостей от Unity UI.
    /// </summary>
    internal sealed class DevToolsNavigation<TPage> where TPage : struct, Enum
    {
        private readonly Stack<TPage> _history = new Stack<TPage>();

        public TPage Current { get; private set; }

        public void Reset(TPage root)
        {
            _history.Clear();
            Current = root;
        }

        public void NavigateTo(TPage page)
        {
            if (EqualityComparer<TPage>.Default.Equals(Current, page))
            {
                return;
            }

            _history.Push(Current);
            Current = page;
        }

        public bool TryGoBack(out TPage page)
        {
            if (_history.Count == 0)
            {
                page = Current;
                return false;
            }

            Current = _history.Pop();
            page = Current;
            return true;
        }
    }
}
#endif
