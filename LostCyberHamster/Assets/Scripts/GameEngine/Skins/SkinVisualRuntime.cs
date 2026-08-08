using System;
using System.Threading.Tasks;
using Assets.Scripts.System.Resources;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Assets.Scripts.GameEngine.Skins
{
    /// <summary>
    /// Владеет instance prefab-визуала и его Addressables lease в течение забега.
    /// </summary>
    public sealed class SkinVisualRuntime : IDisposable
    {
        private readonly SkinVisualHost _host;
        private readonly AddressableLease<GameObject> _prefabLease;
        private GameObject _instance;
        private SkinVisual _visual;
        private bool _disposed;

        private SkinVisualRuntime(
            SkinVisualHost host,
            AddressableLease<GameObject> prefabLease,
            GameObject instance,
            SkinVisual visual)
        {
            _host = host;
            _prefabLease = prefabLease;
            _instance = instance;
            _visual = visual;
        }

        /// <summary>
        /// Загружает prefab, создаёт instance в host и возвращает владельца ресурсов.
        /// </summary>
        public static async Task<SkinVisualRuntime> CreateAsync(string address, SkinVisualHost host)
        {
            if (host == null)
                throw new ArgumentNullException(nameof(host));

            // Lease должен пережить созданный через обычный Instantiate instance.
            AddressableLease<GameObject> lease = await AddressableLoader.LoadAssetAsync<GameObject>(address);
            GameObject instance = null;
            try
            {
                // Instance всегда нормализуется относительно общего skin_slot.
                instance = Object.Instantiate(lease.Value, host.Slot);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;

                // Prefab contract требует SkinVisual на корне.
                SkinVisual visual = instance.GetComponent<SkinVisual>();
                if (visual == null)
                    throw new MissingComponentException($"Skin visual '{address}' has no SkinVisual component.");

                host.Bind(visual);
                return new SkinVisualRuntime(host, lease, instance, visual);
            }
            catch
            {
                if (instance != null)
                    Object.Destroy(instance);
                lease.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Уничтожает instance и освобождает Addressables lease.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _host.Unbind(_visual);
            if (_instance != null)
                Object.Destroy(_instance);

            _instance = null;
            _visual = null;
            _prefabLease.Dispose();
            _disposed = true;
        }
    }
}
