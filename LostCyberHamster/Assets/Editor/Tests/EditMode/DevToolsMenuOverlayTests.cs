#if UNITY_EDITOR
using Assets.Scripts.DevTools;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Tests.EditMode
{
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
                host.AddComponent<DevToolsMenuOverlay>();

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
    }
}
#endif
