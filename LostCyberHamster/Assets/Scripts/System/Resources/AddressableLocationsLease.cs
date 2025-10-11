using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace Assets.Scripts.System.Resources
{
    /// <summary>
    /// Lease для результатов <see cref="Addressables.LoadResourceLocationsAsync"/>.
    /// </summary>
    public sealed class AddressableLocationsLease : IDisposable
    {
        private AsyncOperationHandle<IList<IResourceLocation>> _handle;
        private bool _disposed;

        private AddressableLocationsLease(AsyncOperationHandle<IList<IResourceLocation>> handle, IReadOnlyList<IResourceLocation> locations)
        {
            _handle = handle;
            Locations = locations;
        }

        public IReadOnlyList<IResourceLocation> Locations { get; }

        public AsyncOperationHandle<IList<IResourceLocation>> Handle => _handle;

        public bool IsActive => !_disposed && _handle.IsValid();

        public static AddressableLocationsLease FromHandle(AsyncOperationHandle<IList<IResourceLocation>> handle)
        {
            if (!handle.IsValid())
            {
                throw new InvalidOperationException("AsyncOperationHandle is not valid.");
            }

            var result = handle.Result ?? Array.Empty<IResourceLocation>();
            return new AddressableLocationsLease(handle, result as IReadOnlyList<IResourceLocation> ?? new List<IResourceLocation>(result));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_handle.IsValid())
            {
                Addressables.Release(_handle);
            }

            _handle = default;
            _disposed = true;
            GC.SuppressFinalize(this);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ~AddressableLocationsLease()
        {
            if (!_disposed && _handle.IsValid())
            {
                Debug.LogWarning("[AddressableLocationsLease] Disposed by GC. Ensure Dispose() is called explicitly.");
                try
                {
                    Addressables.Release(_handle);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AddressableLocationsLease] Failed to release handle in finalizer: {ex.Message}");
                }
            }
        }
#endif
    }
}
