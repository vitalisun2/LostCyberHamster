using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Diagnostics
{
    internal sealed class DeviceLogUploadRunner : MonoBehaviour
    {
        private static DeviceLogUploadRunner _instance;

        private readonly Queue<string> _queue = new();
        private bool _isProcessing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (!DeviceLogUploader.IsUploadEnabled())
            {
                return;
            }

            EnsureInstance();
        }

        public static void Enqueue(string reason)
        {
            if (!DeviceLogUploader.IsUploadEnabled())
            {
                return;
            }

            EnsureInstance();
            if (_instance == null)
            {
                return;
            }

            _instance.EnqueueInternal(reason);
        }

        private static void EnsureInstance()
        {
            if (_instance != null)
            {
                return;
            }

            var host = new GameObject(nameof(DeviceLogUploadRunner));
            _instance = host.AddComponent<DeviceLogUploadRunner>();
            DontDestroyOnLoad(host);
        }

        private void EnqueueInternal(string reason)
        {
            _queue.Enqueue(string.IsNullOrWhiteSpace(reason) ? "manual" : reason);

            if (!_isProcessing)
            {
                StartCoroutine(ProcessQueue());
            }
        }

        private IEnumerator ProcessQueue()
        {
            _isProcessing = true;
            while (_queue.Count > 0)
            {
                var reason = _queue.Dequeue();
                yield return DeviceLogUploader.UploadDiagnosticLogCoroutine(reason);
            }

            _isProcessing = false;
        }
    }
}
