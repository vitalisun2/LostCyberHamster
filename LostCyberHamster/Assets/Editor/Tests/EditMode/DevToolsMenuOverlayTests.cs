#if UNITY_EDITOR
using System.Reflection;
using Assets.Scripts.DevTools;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Tests.EditMode
{
    /// <summary>
    /// Проверяет pointer-взаимодействие с общей оболочкой DEV-меню.
    /// </summary>
    [Timeout(5000)]
    public sealed class DevToolsMenuOverlayTests
    {
        [Test]
        public void DevButtonPointerClickOpensPanel()
        {
            GameObject host = new GameObject("DevToolsMenuOverlayTest");
            GameObject eventSystemObject = new GameObject("DevToolsMenuOverlayEventSystemTest");

            try
            {
                EventSystem eventSystem = eventSystemObject.AddComponent<EventSystem>();
                DevToolsMenuOverlay overlay = host.AddComponent<DevToolsMenuOverlay>();
                // Обычный MonoBehaviour не обязан получать Awake автоматически в EditMode test context.
                typeof(DevToolsMenuOverlay)
                    .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(overlay, null);

                Button openButton = host.transform.Find("OpenButton").GetComponent<Button>();
                GameObject panel = host.transform.Find("Panel").gameObject;

                Assert.IsFalse(panel.activeSelf);

                PointerEventData pointerEventData = new PointerEventData(eventSystem)
                {
                    button = PointerEventData.InputButton.Left
                };
                ExecuteEvents.Execute(openButton.gameObject, pointerEventData, ExecuteEvents.pointerClickHandler);

                Assert.IsTrue(panel.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(eventSystemObject);
            }
        }

        [Test]
        public void AccountButtonOpensEmptyScreenAndBackReturnsToRoot()
        {
            GameObject host = new GameObject("DevToolsMenuOverlayNavigationTest");
            GameObject eventSystemObject = new GameObject("DevToolsMenuOverlayNavigationEventSystemTest");

            try
            {
                EventSystem eventSystem = eventSystemObject.AddComponent<EventSystem>();
                DevToolsMenuOverlay overlay = host.AddComponent<DevToolsMenuOverlay>();
                typeof(DevToolsMenuOverlay)
                    .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(overlay, null);

                Transform panel = host.transform.Find("Panel");
                ExecutePointerClick(host.transform.Find("OpenButton").gameObject, eventSystem);
                ExecutePointerClick(
                    panel.Find("RootScreen/RootNavigation/Content/AccountButton").gameObject,
                    eventSystem);

                GameObject accountScreen = panel.Find("AccountScreen").gameObject;
                Assert.IsFalse(panel.Find("RootScreen").gameObject.activeSelf);
                Assert.IsTrue(accountScreen.activeSelf);
                Assert.AreEqual(0, accountScreen.transform.childCount);

                ExecutePointerClick(panel.Find("BackButton").gameObject, eventSystem);

                Assert.IsTrue(panel.Find("RootScreen").gameObject.activeSelf);
                Assert.IsFalse(accountScreen.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(eventSystemObject);
            }
        }

        private static void ExecutePointerClick(GameObject target, EventSystem eventSystem)
        {
            PointerEventData pointerEventData = new PointerEventData(eventSystem)
            {
                button = PointerEventData.InputButton.Left
            };
            ExecuteEvents.Execute(target, pointerEventData, ExecuteEvents.pointerClickHandler);
        }
    }
}
#endif
