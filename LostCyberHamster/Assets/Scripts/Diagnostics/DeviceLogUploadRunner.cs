using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assets.Scripts.Online;
using UnityEngine;

namespace Assets.Scripts.Diagnostics
{
    /// <summary>Хранит ограниченную очередь диагностических снимков между запусками.</summary>
    internal sealed class DeviceLogUploadRunner : MonoBehaviour
    {
        private const int MaxSnapshots = 5;
        private const int MaxSnapshotBytes = 1024 * 1024;
        private static DeviceLogUploadRunner _instance;
        private QueueState _queue = new();
        private IDisposable _registration;
        private string _sendingId;
        private string QueuePath => Path.Combine(Application.persistentDataPath, "diagnostic_upload_queue.json");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (DeviceLogUploader.IsUploadEnabled()) EnsureInstance();
        }

        public static void Enqueue(string reason)
        {
            if (!DeviceLogUploader.IsUploadEnabled()) return;
            EnsureInstance();
            _instance?.EnqueueInternal(string.IsNullOrWhiteSpace(reason) ? "manual" : reason);
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;
            var host = new GameObject(nameof(DeviceLogUploadRunner));
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<DeviceLogUploadRunner>();
        }

        private void Awake()
        {
            // Восстанавливаем очередь без обращения к сети.
            try
            {
                if (File.Exists(QueuePath))
                {
                    if (new FileInfo(QueuePath).Length > MaxSnapshots * MaxSnapshotBytes * 2L)
                        throw new InvalidDataException("Diagnostic queue exceeds its storage limit.");
                    _queue = JsonUtility.FromJson<QueueState>(File.ReadAllText(QueuePath)) ?? new QueueState();
                }
                _queue.Entries ??= new List<QueueEntry>();
                Prune();
                if (File.Exists(QueuePath)) Persist();
            }
            catch (Exception exception)
            {
                _queue = new QueueState();
                DebugManager.DiagStability($"[DEVICE LOG] queue read: {exception.GetType().Name}.");
                try { Persist(); }
                catch (Exception storageException)
                {
                    DebugManager.DiagStability($"[DEVICE LOG] queue recovery: {storageException.GetType().Name}.");
                }
            }
            _registration = OnlineServicesCoordinator.Register("device-logs", FlushAsync,
                () => DeviceLogUploader.IsUploadEnabled() && _queue.Entries.Count > 0);
        }

        private void EnqueueInternal(string reason)
        {
            if (reason.Length > 128) reason = reason.Substring(0, 128);
            if (!DeviceLogUploader.TryPrepareUpload(reason, out var settings, out var payload) ||
                Encoding.UTF8.GetByteCount(payload) > MaxSnapshotBytes) return;

            // Повтор причины обновляет ожидающий снимок, не затрагивая текущую отправку.
            _queue.Entries.RemoveAll(entry => entry.Reason == reason && entry.Id != _sendingId);
            _queue.Entries.Add(new QueueEntry
            {
                Id = Guid.NewGuid().ToString("N"), Reason = reason, Payload = payload,
                Endpoint = settings.endpointUrl, CreatedUtcTicks = DateTime.UtcNow.Ticks
            });
            Prune();
            try
            {
                Persist();
                OnlineServicesCoordinator.RequestRetry("device-logs");
            }
            catch (Exception exception)
            {
                DebugManager.DiagStability($"[DEVICE LOG] queue save: {exception.GetType().Name}.");
            }
        }

        private async Task FlushAsync()
        {
            Prune();
            Persist();
            // Снимок старого endpoint ждёт своего срока хранения; новые отправляются независимо.
            var entry = _queue.Entries.FirstOrDefault(item => DeviceLogUploader.IsCurrentEndpoint(item.Endpoint));
            if (entry == null) return;
            _sendingId = entry.Id;
            try
            {
                await DeviceLogUploader.UploadPreparedAsync(entry.Payload, entry.Endpoint);
                _queue.Entries.RemoveAll(item => item.Id == entry.Id);
                Persist();
                if (_queue.Entries.Count > 0) OnlineServicesCoordinator.RequestRetry("device-logs");
            }
            finally
            {
                _sendingId = null;
            }
        }

        private void Prune()
        {
            long oldest = DateTime.UtcNow.AddDays(-7).Ticks;
            _queue.Entries.RemoveAll(entry => entry == null || entry.CreatedUtcTicks < oldest ||
                string.IsNullOrEmpty(entry.Payload) || Encoding.UTF8.GetByteCount(entry.Payload) > MaxSnapshotBytes);
            while (_queue.Entries.Count > MaxSnapshots) _queue.Entries.RemoveAt(0);
        }

        private void Persist()
        {
            string temporary = QueuePath + ".tmp";
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(temporary, JsonUtility.ToJson(_queue), Encoding.UTF8);
            if (File.Exists(QueuePath)) File.Replace(temporary, QueuePath, null);
            else File.Move(temporary, QueuePath);
        }

        private void OnDestroy()
        {
            _registration?.Dispose();
            if (_instance == this) _instance = null;
        }

        [Serializable]
        private sealed class QueueState
        {
            public List<QueueEntry> Entries = new();
        }

        [Serializable]
        private sealed class QueueEntry
        {
            public string Id;
            public string Reason;
            public string Endpoint;
            public string Payload;
            public long CreatedUtcTicks;
        }
    }
}
