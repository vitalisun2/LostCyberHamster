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
    /// <summary>Хранит целые JSONL-пакеты до подтверждённой записи collector-ом; использует общий сетевой retry.</summary>
    internal sealed class EconomyJournal : IDisposable
    {
        private const int PacketBytes = 128 * 1024;
        private const long QueueBytes = 16 * 1024 * 1024;
        private static readonly Encoding Utf8 = new UTF8Encoding(false);
        private readonly string _root, _active, _endpoint;
        private readonly IDisposable _registration;
        private string _sending;
        public JournalState State { get; private set; } = new();

        public EconomyJournal(string root, string endpoint)
        {
            _root = root;
            _active = Path.Combine(root, "economy_events.jsonl");
            _endpoint = endpoint;
            Directory.CreateDirectory(root);
            // Сбой sidecar не удаляет уже записанные события и очередь.
            try
            {
                if (File.Exists(StatePath)) State = JsonUtility.FromJson<JournalState>(File.ReadAllText(StatePath)) ?? new();
            }
            catch (Exception e)
            {
                State.lost_packets++;
                DebugManager.DiagStability($"[ECO JOURNAL] state recovery: {e.GetType().Name}.");
            }
            if (string.IsNullOrEmpty(State.endpoint)) State.endpoint = endpoint;
            State.dev_profiles ??= new List<string>();
            State.pending_flows ??= Array.Empty<EconomyFlow>();
            // Обрыв последней строки оставляет валидный префикс и явный маркер потери.
            if (File.Exists(_active))
            {
                var content = File.ReadAllText(_active, Utf8);
                if (!content.EndsWith("\n", StringComparison.Ordinal) && content.Length > 0)
                {
                    File.WriteAllText(_active, content.Substring(0, content.LastIndexOf('\n') + 1), Utf8);
                    State.lost_packets++;
                }
            }
            SaveState();
            _registration = OnlineServicesCoordinator.Register("economy-journal", FlushAsync,
                () => DeviceLogUploader.IsUploadEnabled() && State.endpoint == _endpoint &&
                    (File.Exists(_active) || Directory.EnumerateFiles(_root, "*.packet.jsonl").Any()));
        }

        private string StatePath => Path.Combine(_root, "state.json");

        public void Append(string json)
        {
            if (File.Exists(_active) && new FileInfo(_active).Length + Utf8.GetByteCount(json) > PacketBytes) Seal();
            Prune();
            var item = JsonUtility.FromJson<EconomyEvent>(json);
            if (Utf8.GetByteCount(json) > PacketBytes)
            {
                State.lost_packets++;
                item.type = "data_gap";
                item.source = "event_exceeds_packet_limit";
                item.before = null;
                item.after = null;
                item.flows = null;
                item.detail = null;
                item.confirmed = false;
            }
            item.lost_packets = State.lost_packets;
            File.AppendAllText(_active, JsonUtility.ToJson(item) + "\n", Utf8);
            SaveState();
        }

        private void Seal()
        {
            if (!File.Exists(_active) || new FileInfo(_active).Length == 0) return;
            File.Move(_active, Path.Combine(_root, DateTime.UtcNow.Ticks + "_" + Guid.NewGuid().ToString("N") + ".packet.jsonl"));
        }

        public void RequestUpload() => OnlineServicesCoordinator.RequestRetry("economy-journal");

        private async Task FlushAsync()
        {
            // Пакет фиксирован на время запроса; новые события пишутся в следующий файл.
            Seal();
            var packet = Directory.EnumerateFiles(_root, "*.packet.jsonl").OrderBy(x => x, StringComparer.Ordinal).FirstOrDefault();
            if (packet == null) return;
            _sending = packet;
            try
            {
                var data = File.ReadAllText(packet, Utf8);
                var id = EconomySnapshot.Digest(data);
                var payload = DeviceLogUploader.BuildEconomyPayload(data, id);
                await DeviceLogUploader.UploadPreparedAsync(payload, _endpoint, id);
                File.Delete(packet);
                RequestUpload();
            }
            finally { _sending = null; }
        }

        private void Prune()
        {
            var files = Directory.EnumerateFiles(_root, "*.jsonl").Select(x => new FileInfo(x)).ToArray();
            long size = files.Sum(x => x.Length);
            foreach (var file in files.Where(x => x.FullName != _active && x.FullName != _sending).OrderBy(x => x.Name))
            {
                if (size <= QueueBytes - PacketBytes) break;
                size -= file.Length;
                file.Delete();
                State.lost_packets++;
            }
        }

        public void SaveState()
        {
            string temporary = StatePath + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(State), Utf8);
            if (File.Exists(StatePath)) File.Replace(temporary, StatePath, null);
            else File.Move(temporary, StatePath);
        }

        public void Dispose() => _registration?.Dispose();

        [Serializable]
        internal sealed class JournalState
        {
            public string profile, run, level, endpoint, cohort, run_close_hint;
            public double active;
            public long sequence, lost_packets;
            public int remaining_lives = -1;
            public EconomySnapshot snapshot;
            public EconomySnapshot run_snapshot;
            public EconomyFlow[] pending_flows = Array.Empty<EconomyFlow>();
            public List<string> dev_profiles = new();
        }
    }
}
