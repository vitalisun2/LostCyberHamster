using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using Assets.Scripts.System.Resources;
using LostCyberHamster.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Assets.Tests.UI
{
    [TestFixture]
    public sealed class PreparedScreenInputBlockTests
    {
        [Test]
        public void SetInputBlocked_SeparateOwnersKeepBlockUntilLastOwnerReleased()
        {
            using var prepared = CreatePreparedScreen();
            var ownerA = new object();
            var ownerB = new object();

            InvokeSetInputBlocked(prepared, ownerA, true);
            Assert.AreEqual(1, GetOwnerCount(prepared));

            InvokeSetInputBlocked(prepared, ownerB, true);
            Assert.AreEqual(2, GetOwnerCount(prepared));

            InvokeSetInputBlocked(prepared, ownerA, false);
            Assert.AreEqual(1, GetOwnerCount(prepared));

            InvokeSetInputBlocked(prepared, ownerB, false);
            Assert.AreEqual(0, GetOwnerCount(prepared));
        }

        [Test]
        public void SetInputBlocked_RepeatingSameOwnerDoesNotDuplicateRegistration()
        {
            using var prepared = CreatePreparedScreen();
            var owner = new object();

            InvokeSetInputBlocked(prepared, owner, true);
            InvokeSetInputBlocked(prepared, owner, true);
            Assert.AreEqual(1, GetOwnerCount(prepared));

            InvokeSetInputBlocked(prepared, owner, false);
            Assert.AreEqual(0, GetOwnerCount(prepared));
        }

        [Test]
        public void LoadScreenAsync_SameScreenRequestClosesActiveModal()
        {
            var document = CreateDocument();
            using var prepared = CreatePreparedScreen();
            var screenController = new TestScreenController(document);
            var modalController = new TestModalController(document);
            var modalContent = document.rootVisualElement.Q<VisualElement>("modal__content");
            modalContent.Add(new Label("marker"));

            SetPrivateField(screenController, "_screen", prepared);
            InvokeSetModalInputBlocked(screenController, true);

            var manager = new UIManager(new IScreenController[]
            {
                screenController,
                modalController
            });
            SetPrivateField(manager, "_currentScreen", ScreenEnum.HomeScreen);
            SetPrivateField(manager, "_hasCurrentScreen", true);
            SetPrivateField(manager, "_activeScreenEventsSubscribed", true);
            SetPrivateField(manager, "_currentModal", ScreenEnum.AccountPromptModal);

            InvokeLoadScreenAsync(manager, ScreenEnum.HomeScreen, forceReload: false, closeActiveModal: true);

            Assert.IsNull(GetPrivateField(manager, "_currentModal"));
            Assert.AreEqual(0, GetOwnerCount(prepared));
            Assert.AreEqual(0, modalContent.childCount);

            UnityEngine.Object.DestroyImmediate(document.gameObject);
        }

        [UnityTest]
        public IEnumerator InputTransitionBoundary_BlocksOnlyUntilDeferredTransitionCompletes()
        {
            var transitionStarted = false;
            var boundaryType = typeof(UIManager).Assembly.GetType(
                "LostCyberHamster.UI.UiInputCarryoverBlock",
                throwOnError: true);
            var method = boundaryType.GetMethod(
                "RunAfterCurrentEventAsync",
                BindingFlags.Static | BindingFlags.Public);
            var task = (Task)method.Invoke(null, new object[]
            {
                (Func<Task>)(() =>
                {
                    transitionStarted = true;
                    return Task.CompletedTask;
                })
            });

            Assert.IsTrue(UiInputBlock.IsBlocked);
            Assert.IsFalse(transitionStarted);

            while (!task.IsCompleted)
                yield return null;

            Assert.IsFalse(task.IsFaulted);
            Assert.IsTrue(transitionStarted);
            Assert.IsFalse(UiInputBlock.IsBlocked);
        }

        private static IDisposable CreatePreparedScreen()
        {
            var preparedType = GetPreparedScreenType();
            var handle = Addressables.ResourceManager.CreateCompletedOperation(
                new VisualTreeAsset(),
                null);
            var lease = AddressableLease<VisualTreeAsset>.FromHandle(handle);
            return (IDisposable)Activator.CreateInstance(
                preparedType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { lease },
                culture: null);
        }

        private static void InvokeSetInputBlocked(
            IDisposable prepared,
            object owner,
            bool blocked)
        {
            var method = GetPreparedScreenType().GetMethod(
                "SetInputBlocked",
                BindingFlags.Instance | BindingFlags.Public);
            method.Invoke(prepared, new[] { owner, (object)blocked });
        }

        private static int GetOwnerCount(IDisposable prepared)
        {
            var field = GetPreparedScreenType().GetField(
                "_inputBlockOwners",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var owners = field.GetValue(prepared);
            return (int)owners.GetType().GetProperty("Count").GetValue(owners);
        }

        private static Type GetPreparedScreenType()
        {
            return typeof(ScreenController).Assembly.GetType(
                "LostCyberHamster.UI.PreparedScreen",
                throwOnError: true);
        }

        private static UIDocument CreateDocument()
        {
            var gameObject = new GameObject("ui-test-document");
            var document = gameObject.AddComponent<UIDocument>();
            var root = document.rootVisualElement;
            root.Add(new VisualElement { name = "background" });
            root.Add(new VisualElement { name = "content" });
            root.Add(new Button { name = "btn_close-modal" });
            var modal = new VisualElement { name = "modal" };
            modal.Add(new VisualElement { name = "modal__content" });
            root.Add(modal);
            return document;
        }

        private static void InvokeSetModalInputBlocked(
            ScreenController screenController,
            bool blocked)
        {
            var method = typeof(ScreenController).GetMethod(
                "SetModalInputBlocked",
                BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(screenController, new object[] { blocked });
        }

        private static void InvokeLoadScreenAsync(
            UIManager manager,
            ScreenEnum screen,
            bool forceReload,
            bool closeActiveModal)
        {
            var method = typeof(UIManager).GetMethod(
                "LoadScreenAsync",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(ScreenEnum), typeof(bool), typeof(bool) },
                modifiers: null);
            var task = (Task)method.Invoke(
                manager,
                new object[] { screen, forceReload, closeActiveModal });
            task.GetAwaiter().GetResult();
        }

        private static object GetPrivateField(object instance, string name)
        {
            return instance.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
        }

        private static void SetPrivateField(object instance, string name, object value)
        {
            var type = instance.GetType();
            while (type != null)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    field.SetValue(instance, value);
                    return;
                }

                type = type.BaseType;
            }

            throw new MissingFieldException(instance.GetType().FullName, name);
        }

        private sealed class TestScreenController : ScreenController
        {
            public TestScreenController(UIDocument document)
                : base(document)
            {
            }

            protected override ScreenEnum _screenAssetName => ScreenEnum.HomeScreen;

            protected override void BindView()
            {
            }

            protected override void OnSubscribeToEvents()
            {
            }

            protected override void OnUnsubscribeFromEvents()
            {
            }
        }

        private sealed class TestModalController : ModalController
        {
            public TestModalController(UIDocument document)
                : base(document)
            {
            }

            protected override ScreenEnum _modalAssetName => ScreenEnum.AccountPromptModal;

            protected override Task OnShowAsync()
            {
                return Task.CompletedTask;
            }

            protected override void OnSubscribeToEvents()
            {
            }

            protected override void OnUnsubscribeFromEvents()
            {
            }
        }
    }
}
