using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Assets.Scripts.System.Resources
{
    /// <summary>
    /// RAII-обёртка над <see cref="AsyncOperationHandle{TObject}"/>. Гарантирует, что хэндл будет освобождён, когда lease покинет область видимости.
    /// </summary>
    /// <typeparam name="T">Тип загруженного ассета.</typeparam>
    public sealed class AddressableLease<T> : IDisposable
    {
        private AsyncOperationHandle<T> _handle;
        private bool _disposed;

        private AddressableLease(AsyncOperationHandle<T> handle, T value)
        {
            _handle = handle;
            Value = value;
        }

        /// <summary>
        /// Загруженный ассет.
        /// </summary>
        public T Value { get; }

        /// <summary>
        /// Текущий хэндл Addressables (для диагностики).
        /// </summary>
        public AsyncOperationHandle<T> Handle => _handle;

        /// <summary>
        /// Возвращает <c>true</c>, если lease всё ещё держит валидный хэндл.
        /// </summary>
        public bool IsActive => !_disposed && _handle.IsValid();

        /// <summary>
        /// Создаёт lease на основе завершённого хэндла.
        /// </summary>
        public static AddressableLease<T> FromHandle(AsyncOperationHandle<T> handle)
        {
            if (!handle.IsValid())
            {
                throw new InvalidOperationException("AsyncOperationHandle is not valid.");
            }

            return new AddressableLease<T>(handle, handle.Result);
        }

        /// <summary>
        /// Освобождает удерживаемый хэндл.
        /// </summary>
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
        ~AddressableLease()
        {
            if (!_disposed && _handle.IsValid())
            {
                Debug.LogWarning($"[AddressableLease<{typeof(T).Name}>] Disposed by GC. Ensure Dispose() is called explicitly.");
                try
                {
                    Addressables.Release(_handle);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AddressableLease<{typeof(T).Name}>] Failed to release handle in finalizer: {ex.Message}");
                }
            }
        }
#endif
    }
}
