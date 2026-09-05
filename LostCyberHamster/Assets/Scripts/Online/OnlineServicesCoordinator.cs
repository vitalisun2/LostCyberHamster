using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;
using Vues.GameCore;

namespace Assets.Scripts.Online
{
    /// <summary>Восстанавливает независимые сетевые сервисы в фоне, сохраняя доступность локальной игры.</summary>
    public static class OnlineServicesCoordinator
    {
        private static readonly Dictionary<string, Job> Jobs = new(StringComparer.Ordinal);
        private static readonly double[] RetryDelays = { 5, 15, 30, 60 };
        private static OnlineServicesRunner _runner;
        private static bool _quitting;
        private static int _generation;

        public static bool UnityServicesReady => UnityServices.State == ServicesInitializationState.Initialized;
        public static string EnvironmentName => Application.isEditor || Debug.isDebugBuild ? "development" : "production";

        /// <summary>Регистрирует одну последовательную операцию; повтор регистрации заменяет прежнего владельца.</summary>
        public static IDisposable Register(string name, Func<Task> operation, Func<bool> canRun = null)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException(nameof(name));
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            EnsureRunner();
            if (Jobs.TryGetValue(name, out var previous)) previous.Dispose();
            var job = new Job(name, operation, canRun);
            Jobs[name] = job;
            return job;
        }

        /// <summary>Планирует немедленный повтор, объединяя запросы во время текущей операции.</summary>
        public static void RequestRetry(string name)
        {
            if (!Jobs.TryGetValue(name, out var job)) return;
            job.NextAttempt = 0;
            job.Requested = true;
        }

        /// <summary>Запускает инициализацию UGS отдельно от обязательной загрузки игры.</summary>
        public static void StartUnityServices()
        {
            if (Jobs.ContainsKey("ugs")) return;
            Register("ugs", () => UnityServices.InitializeAsync(
                new InitializationOptions().SetEnvironmentName(EnvironmentName)), () => !UnityServicesReady);
        }

        internal static void Tick()
        {
            if (_quitting) return;
            var now = UnityGameClock.Instance.RealtimeSeconds;
            foreach (var job in Jobs.Values.ToArray())
            {
                if (job.Disposed || job.Running || now < job.NextAttempt) continue;
                try
                {
                    if (job.CanRun != null && !job.CanRun()) continue;
                    job.Running = true;
                    job.Requested = false;
                    _ = ExecuteAsync(job, _generation);
                }
                catch (Exception exception)
                {
                    job.NextAttempt = now + 5;
                    DebugManager.DiagStability($"[ONLINE] {job.Name} readiness: {exception.GetType().Name}.");
                }
            }
        }

        internal static void Resume()
        {
            foreach (var job in Jobs.Values)
            {
                job.NextAttempt = 0;
                job.Requested = true;
            }
        }

        internal static void Quit() => _quitting = true;

        private static async Task ExecuteAsync(Job job, int generation)
        {
            double delay = 60;
            try
            {
                await job.Operation();
                job.Failures = 0;
            }
            catch (Exception exception)
            {
                delay = RetryDelays[Math.Min(job.Failures++, RetryDelays.Length - 1)];
                delay = Math.Min(60, delay * UnityEngine.Random.Range(0.9f, 1.1f));
                if (generation == _generation && !job.Disposed)
                    DebugManager.DiagStability($"[ONLINE] {job.Name} retry; error={exception.GetType().Name}.");
            }
            finally
            {
                job.Running = false;
                if (generation == _generation && !job.Disposed)
                    job.NextAttempt = job.Requested ? 0 : UnityGameClock.Instance.RealtimeSeconds + delay;
            }
        }

        private static void EnsureRunner()
        {
            if (_runner != null || _quitting || !Application.isPlaying) return;
            var host = new GameObject("[OnlineServices]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            _runner = host.AddComponent<OnlineServicesRunner>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _generation++;
            foreach (var job in Jobs.Values.ToArray()) job.Dispose();
            Jobs.Clear();
            _runner = null;
            _quitting = false;
        }

        private sealed class Job : IDisposable
        {
            public readonly string Name;
            public readonly Func<Task> Operation;
            public readonly Func<bool> CanRun;
            public double NextAttempt;
            public int Failures;
            public bool Running;
            public bool Requested;
            public bool Disposed;

            public Job(string name, Func<Task> operation, Func<bool> canRun)
            {
                Name = name;
                Operation = operation;
                CanRun = canRun;
            }

            public void Dispose()
            {
                Disposed = true;
                if (Jobs.TryGetValue(Name, out var current) && ReferenceEquals(current, this))
                    Jobs.Remove(Name);
            }
        }
    }
}
