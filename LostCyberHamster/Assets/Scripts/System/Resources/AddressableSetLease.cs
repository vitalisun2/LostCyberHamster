using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Assets.Scripts.System.Resources
{
    /// <summary>
    /// RAII-обёртка для результатов загрузки набора ассетов (LoadAssetsAsync).
    /// </summary>
    /// <typeparam name="T">Тип загруженных ассетов.</typeparam>
    public sealed class AddressableSetLease<T> : IDisposable
    {
        private AsyncOperationHandle<IList<T>> _handle;
        private bool _disposed;

        private AddressableSetLease(AsyncOperationHandle<IList<T>> handle, IReadOnlyList<T> values)
        {
            _handle = handle;
            Values = values;
        }

        /// <summary>
        /// Загруженные ассеты (бессобытийная коллекция). Ссылка остаётся валидной до вызова <see cref="Dispose"/>.
        /// </summary>
        public IReadOnlyList<T> Values { get; }

        public AsyncOperationHandle<IList<T>> Handle => _handle;

        public bool IsActive => !_disposed && _handle.IsValid();

        public static AddressableSetLease<T> FromHandle(AsyncOperationHandle<IList<T>> handle)
        {
            if (!handle.IsValid())
            {
                throw new InvalidOperationException("AsyncOperationHandle is not valid.");
            }

            var result = handle.Result ?? Array.Empty<T>();
            return new AddressableSetLease<T>(handle, result as IReadOnlyList<T> ?? new List<T>(result));
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
        ~AddressableSetLease()
        {
            if (!_disposed && _handle.IsValid())
            {
                Debug.LogWarning($"[AddressableSetLease<{typeof(T).Name}>] Disposed by GC. Ensure Dispose() is called explicitly.");
                // В finalizer мы не на main thread, поэтому вызывать Unity API (Addressables.Release)
                // здесь нельзя: это приводит к InvalidOperationException.
                // Осознанно пропускаем release и оставляем диагностический warning.
            }
        }
#endif
    }
}
