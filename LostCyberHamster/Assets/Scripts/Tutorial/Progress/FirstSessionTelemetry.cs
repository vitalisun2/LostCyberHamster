using System;
using Assets.Scripts.System;
using UnityEngine;

namespace Assets.Scripts.Tutorial
{
    /// <summary>Фиксирует значимые переходы первой сессии; дедупликация игровых фактов принадлежит их владельцам.</summary>
    public static class FirstSessionTelemetry
    {
        private static string _sessionId = Guid.NewGuid().ToString("N");
        private static int _sequence;
        private static double _startedAt = -1;

        /// <summary>Записывает один переход; detail содержит технический ID или источник, а не пользовательский текст.</summary>
        public static void Record(string phase, string detail = null, int value = 0)
        {
            if (string.IsNullOrWhiteSpace(phase)) return;
            try
            {
                // Один вызов соответствует одному факту, а session/sequence различают повторные попытки.
                phase = Limit(phase.Trim(), 64);
                detail = Limit(detail ?? string.Empty, 256);
                double now = Time.realtimeSinceStartupAsDouble;
                if (_startedAt < 0) _startedAt = now;
                int sequence = ++_sequence;

                // DEV и test-level остаются в диагностике устройства и не попадают в production-воронку.
                if (Application.isEditor || Debug.isDebugBuild || AutomationRuntimePrefs.IsTestLevelAutomationRun())
                {
                    DebugManager.DiagEconomy($"[FirstSession] phase={phase} detail={detail} value={value} " +
                        $"sequence={sequence} elapsed={now - _startedAt:0.00} cohort=development");
                    return;
                }
                AnalyticsManager.RecordFirstSession(phase, detail, value, _sessionId, sequence, now - _startedAt);
            }
            catch (Exception exception)
            {
                DebugManager.DiagStability($"[FirstSession] Telemetry unavailable ({exception.GetType().Name}).");
            }
        }

        private static string Limit(string value, int length) => value.Length <= length ? value : value.Substring(0, length);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _sessionId = Guid.NewGuid().ToString("N");
            _sequence = 0;
            _startedAt = -1;
        }
    }
}
