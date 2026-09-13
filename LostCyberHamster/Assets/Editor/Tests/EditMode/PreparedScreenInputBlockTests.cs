using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using Assets.Scripts.System.Resources;
using LostCyberHamster.UI;
using NUnit.Framework;
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
    }
}