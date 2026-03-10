using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace Assets.Scripts.System.Resources
{
    /// <summary>
    /// Универсальный загрузчик Addressables, возвращающий lease-обёртки вместо «сырых» хэндлов.
    /// </summary>
    public static class AddressableLoader
    {
        public static async Task<AddressableLease<T>> LoadAssetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key must be provided", nameof(key));
            }

            cancellationToken.ThrowIfCancellationRequested();

            AsyncOperationHandle<T> handle = default;
            try
            {
                handle = Addressables.LoadAssetAsync<T>(key);
                var result = await handle.Task.ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();
                return AddressableLease<T>.FromHandle(handle);
            }
            catch
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }

                throw;
            }
        }

        public static async Task<AddressableSetLease<T>> LoadAssetsByLabelAsync<T>(string label, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                throw new ArgumentException("Label must be provided", nameof(label));
            }

            cancellationToken.ThrowIfCancellationRequested();

            AsyncOperationHandle<IList<T>> handle = default;
            try
            {
                handle = Addressables.LoadAssetsAsync<T>(label, null);
                var _ = await handle.Task.ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();
                return AddressableSetLease<T>.FromHandle(handle);
            }
            catch
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }

                throw;
            }
        }

        /// <summary>
        /// Синхронная версия <see cref="LoadAssetAsync{T}"/>. Использовать только в редакторском коде.
        /// </summary>
        public static AddressableLease<T> LoadAssetSync<T>(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key must be provided", nameof(key));
            }

            var handle = Addressables.LoadAssetAsync<T>(key);
            try
            {
                var _ = handle.WaitForCompletion();
                return AddressableLease<T>.FromHandle(handle);
            }
            catch
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }

                throw;
            }
        }

        /// <summary>
        /// Синхронная версия <see cref="LoadAssetsByLabelAsync{T}"/>. Использовать только в редакторском коде.
        /// </summary>
        public static AddressableSetLease<T> LoadAssetsByLabelSync<T>(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                throw new ArgumentException("Label must be provided", nameof(label));
            }

            var handle = Addressables.LoadAssetsAsync<T>(label, null);
            try
            {
                var _ = handle.WaitForCompletion();
                return AddressableSetLease<T>.FromHandle(handle);
            }
            catch
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }

                throw;
            }
        }

        /// <summary>
        /// Checks whether any assets exist for the given label. Editor-only synchronous call.
        /// </summary>
        public static bool HasAssetsForLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
                return false;

            var handle = Addressables.LoadResourceLocationsAsync(label);
            var locations = handle.WaitForCompletion();
            bool hasAssets = locations != null && locations.Count > 0;
            Addressables.Release(handle);
            return hasAssets;
        }

        public static async Task<AddressableLocationsLease> LoadLocationsAsync(string label, Type assetType, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                throw new ArgumentException("Label must be provided", nameof(label));
            }

            assetType ??= typeof(object);

            cancellationToken.ThrowIfCancellationRequested();

            AsyncOperationHandle<IList<IResourceLocation>> handle = default;
            try
            {
                handle = Addressables.LoadResourceLocationsAsync(label, assetType);
                var _ = await handle.Task.ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();
                return AddressableLocationsLease.FromHandle(handle);
            }
            catch
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }

                throw;
            }
        }
    }
}
