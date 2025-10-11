using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Assets.Scripts.System.Resources;
using NUnit.Framework;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace Assets.Tests.System.Resources
{
    [TestFixture]
    public sealed class AddressableLeaseTests
    {
        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            Addressables.InitializeAsync().WaitForCompletion();
        }

        [Test]
        public void AddressableLease_FromInvalidHandle_Throws()
        {
            var invalidHandle = default(AsyncOperationHandle<string>);
            Assert.Throws<InvalidOperationException>(() => AddressableLease<string>.FromHandle(invalidHandle));
        }

        [Test]
        public void AddressableLease_DisposeReleasesHandle()
        {
            var value = "test";
            var handle = Addressables.ResourceManager.CreateCompletedOperation(value, null);

            using var lease = AddressableLease<string>.FromHandle(handle);
            Assert.AreEqual(value, lease.Value);
            Assert.IsTrue(lease.IsActive);

            lease.Dispose();

            Assert.IsFalse(lease.IsActive);
            Assert.IsFalse(lease.Handle.IsValid());
        }

        [Test]
        public void AddressableLease_DisposeTwice_DoesNotThrow()
        {
            var handle = Addressables.ResourceManager.CreateCompletedOperation("value", null);
            var lease = AddressableLease<string>.FromHandle(handle);

            Assert.DoesNotThrow(() =>
            {
                lease.Dispose();
                lease.Dispose();
            });
        }

        [Test]
        public void AddressableSetLease_FromHandle_PopulatesValues()
        {
            var values = new List<int> { 1, 2, 3 };
            var handle = Addressables.ResourceManager.CreateCompletedOperation<IList<int>>(values, null);

            using var lease = AddressableSetLease<int>.FromHandle(handle);
            CollectionAssert.AreEqual(values, lease.Values);
            Assert.IsTrue(lease.IsActive);

            lease.Dispose();
            Assert.IsFalse(lease.IsActive);
        }

        [Test]
        public void AddressableSetLease_DisposeTwice_DoesNotThrow()
        {
            var handle = Addressables.ResourceManager.CreateCompletedOperation<IList<int>>(new List<int> { 1 }, null);
            var lease = AddressableSetLease<int>.FromHandle(handle);

            Assert.DoesNotThrow(() =>
            {
                lease.Dispose();
                lease.Dispose();
            });
        }

        [Test]
        public void AddressableLocationsLease_FromHandle_PopulatesLocations()
        {
            var locations = new List<IResourceLocation>
            {
                new DummyLocation("locA"),
                new DummyLocation("locB")
            };

            var handle = Addressables.ResourceManager.CreateCompletedOperation<IList<IResourceLocation>>(locations, null);

            using var lease = AddressableLocationsLease.FromHandle(handle);
            CollectionAssert.AreEqual(locations, lease.Locations);
            Assert.IsTrue(lease.IsActive);

            lease.Dispose();
            Assert.IsFalse(lease.IsActive);
        }

        [Test]
        public void AddressableLocationsLease_DisposeTwice_DoesNotThrow()
        {
            var handle = Addressables.ResourceManager.CreateCompletedOperation<IList<IResourceLocation>>(new List<IResourceLocation>(), null);
            var lease = AddressableLocationsLease.FromHandle(handle);

            Assert.DoesNotThrow(() =>
            {
                lease.Dispose();
                lease.Dispose();
            });
        }

        [Test]
        public void LoadAssetSync_WithEmptyKey_Throws()
        {
            Assert.Throws<ArgumentException>(() => AddressableLoader.LoadAssetSync<object>(" "));
        }

        [Test]
        public void LoadAssetsByLabelSync_WithEmptyLabel_Throws()
        {
            Assert.Throws<ArgumentException>(() => AddressableLoader.LoadAssetsByLabelSync<object>(null));
        }

        [Test]
        public async Task LoadAssetAsync_WithEmptyKey_Throws()
        {
            await Assert.ThrowsAsync<ArgumentException>(async () => await AddressableLoader.LoadAssetAsync<object>(null));
        }

        [Test]
        public async Task LoadAssetsByLabelAsync_WithEmptyLabel_Throws()
        {
            await Assert.ThrowsAsync<ArgumentException>(async () => await AddressableLoader.LoadAssetsByLabelAsync<object>(string.Empty));
        }

        [Test]
        public async Task LoadLocationsAsync_WithEmptyLabel_Throws()
        {
            await Assert.ThrowsAsync<ArgumentException>(async () => await AddressableLoader.LoadLocationsAsync("", typeof(object)));
        }

        private sealed class DummyLocation : IResourceLocation
        {
            public DummyLocation(string id)
            {
                InternalId = id;
                PrimaryKey = id;
                ProviderId = "dummy";
                ResourceType = typeof(object);
                Dependencies = Array.Empty<IResourceLocation>();
            }

            public string InternalId { get; }

            public string ProviderId { get; }

            public IList<IResourceLocation> Dependencies { get; }

            public bool HasDependencies => Dependencies.Count > 0;

            public int DependencyHashCode => 0;

            public object Data => null;

            public string PrimaryKey { get; }

            public Type ResourceType { get; }
        }
    }
}
