#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace Assets.Scripts.DevTools.Core
{
    /// <summary>
    /// Определяет lifecycle feature-экрана, размещаемого внутри общего DEV-shell.
    /// </summary>
    internal interface IDevToolsScreen
    {
        GameObject RootObject { get; }
        void Show();
        void Hide();
        void GoBack();
        void ApplyLayout(float left, float top, float right, float bottom);
        void RefreshPresentation();
    }
}
#endif
