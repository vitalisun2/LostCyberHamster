using UnityEngine;
using UnityEngine.UIElements;

namespace LostCyberHamster.UI
{
    /// <summary>Перехватывает ввод всего документа и переподключается при пересоздании его корня.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class UiDocumentInputGuard : MonoBehaviour
    {
        private UIDocument _document;
        private VisualElement _root;

        internal static void EnsureAttached(UIDocument document)
        {
            if (document.GetComponent<UiDocumentInputGuard>() == null)
                document.gameObject.AddComponent<UiDocumentInputGuard>();
        }

        private void OnEnable()
        {
            _document = GetComponent<UIDocument>();
            Update();
        }

        private void Update()
        {
            var root = _document != null && _document.isActiveAndEnabled
                ? _document.rootVisualElement : null;
            if (_root == root)
                return;
            SetCallbacks(false);
            _root = root;
            SetCallbacks(true);
        }

        private void OnDisable()
        {
            SetCallbacks(false);
            _root = null;
        }

        private void SetCallbacks(bool register)
        {
            if (_root == null)
                return;

            if (register)
            {
                _root.RegisterCallback<PointerDownEvent>(Block, TrickleDown.TrickleDown);
                _root.RegisterCallback<PointerUpEvent>(Block, TrickleDown.TrickleDown);
                _root.RegisterCallback<PointerMoveEvent>(Block, TrickleDown.TrickleDown);
                _root.RegisterCallback<ClickEvent>(Block, TrickleDown.TrickleDown);
                _root.RegisterCallback<WheelEvent>(Block, TrickleDown.TrickleDown);
                _root.RegisterCallback<KeyDownEvent>(Block, TrickleDown.TrickleDown);
                _root.RegisterCallback<KeyUpEvent>(Block, TrickleDown.TrickleDown);
                _root.RegisterCallback<NavigationSubmitEvent>(Block, TrickleDown.TrickleDown);
                _root.RegisterCallback<NavigationCancelEvent>(Block, TrickleDown.TrickleDown);
                _root.RegisterCallback<NavigationMoveEvent>(Block, TrickleDown.TrickleDown);
            }
            else
            {
                _root.UnregisterCallback<PointerDownEvent>(Block, TrickleDown.TrickleDown);
                _root.UnregisterCallback<PointerUpEvent>(Block, TrickleDown.TrickleDown);
                _root.UnregisterCallback<PointerMoveEvent>(Block, TrickleDown.TrickleDown);
                _root.UnregisterCallback<ClickEvent>(Block, TrickleDown.TrickleDown);
                _root.UnregisterCallback<WheelEvent>(Block, TrickleDown.TrickleDown);
                _root.UnregisterCallback<KeyDownEvent>(Block, TrickleDown.TrickleDown);
                _root.UnregisterCallback<KeyUpEvent>(Block, TrickleDown.TrickleDown);
                _root.UnregisterCallback<NavigationSubmitEvent>(Block, TrickleDown.TrickleDown);
                _root.UnregisterCallback<NavigationCancelEvent>(Block, TrickleDown.TrickleDown);
                _root.UnregisterCallback<NavigationMoveEvent>(Block, TrickleDown.TrickleDown);
            }
        }

        private static void Block(EventBase evt)
        {
            if (UiInputBlock.IsBlocked)
                evt.StopImmediatePropagation();
        }
    }
}
