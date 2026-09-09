using System.Threading.Tasks;
using Assets.Scripts.System.Resources;
using UnityEngine;

namespace LostCyberHamster.UI
{
    /// <summary>Загружает локальный Addressable JSON независимо от правил выбора целей.</summary>
    internal static class NextGoalConfigurationLoader
    {
        public const string Address = "next_goal_cards";

        public static async Task<NextGoalConfiguration> LoadAsync()
        {
            using var lease = await AddressableLoader.LoadAssetAsync<TextAsset>(Address);
            var configuration = JsonUtility.FromJson<NextGoalConfiguration>(lease.Value.text);
            if (configuration == null) throw new System.InvalidOperationException("Missing next-goal configuration.");
            configuration.Validate();
            return configuration;
        }
    }
}
