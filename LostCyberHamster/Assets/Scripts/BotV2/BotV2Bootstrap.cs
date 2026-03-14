using Assets.Scripts.Bot;
using UnityEngine;

namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Подключает BotV2 как основной бот и отключает legacy HamsterBot.
    /// Управление через PlayerPrefs:
    /// BotV2PrimaryEnabled = 1 (по умолчанию) — BotV2 основной,
    /// BotV2PrimaryEnabled = 0 — используется legacy-режим.
    /// </summary>
    public static class BotV2Bootstrap
    {
        public const string PrimaryBotPrefKey = "BotV2PrimaryEnabled";

        /// <summary>
        /// Флаг: должен ли BotV2 быть основным ботом.
        /// </summary>
        public static bool UseBotV2AsPrimary => PlayerPrefs.GetInt(PrimaryBotPrefKey, 1) == 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsurePrimaryBot()
        {
            if (!UseBotV2AsPrimary) return;

            DisableLegacyBot();
            EnsureOrchestrator().EnableAsPrimary();
        }

        private static void DisableLegacyBot()
        {
            var legacyBots = Object.FindObjectsOfType<HamsterBot>(true);
            for (int i = 0; i < legacyBots.Length; i++)
            {
                if (legacyBots[i] == null) continue;
                legacyBots[i].enabled = false;
            }
        }

        private static BotOrchestrator EnsureOrchestrator()
        {
            var existing = Object.FindObjectOfType<BotOrchestrator>(true);
            if (existing != null)
            {
                existing.enabled = true;
                return existing;
            }

            var host = GameObject.Find("[HamsterBot]");
            if (host == null)
                host = new GameObject("[BotV2]");

            var orchestrator = host.GetComponent<BotOrchestrator>();
            if (orchestrator == null)
                orchestrator = host.AddComponent<BotOrchestrator>();

            orchestrator.enabled = true;
            return orchestrator;
        }
    }
}
