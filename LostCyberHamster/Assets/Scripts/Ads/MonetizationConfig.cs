using UnityEngine;

namespace GameAds
{
    /// <summary>Отдельное включение эксперимента и магазина после настройки внешних сервисов.</summary>
    [CreateAssetMenu(menuName = "Lost Cyber Hamster/Monetization")]
    public sealed class MonetizationConfig : ScriptableObject
    {
        public bool EnableInterstitial;
        [Range(0, 100)] public int InterstitialVariantPercent = 50;
        public bool EnablePurchases;
        public bool AdsTestMode = true;
        public const int MinimumWins = 3;
        public const double MinimumActiveSeconds = 15 * 60;
        public const int MaximumInterstitialsPerDay = 1;
        private static MonetizationConfig _current;
        public static MonetizationConfig Current => _current != null ? _current :
            _current = Resources.Load<MonetizationConfig>("MonetizationConfig") ?? CreateInstance<MonetizationConfig>();
    }
}
