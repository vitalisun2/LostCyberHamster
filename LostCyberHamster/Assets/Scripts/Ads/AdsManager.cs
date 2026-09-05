namespace GameAds
{
    /// <summary>Сохраняет точки запуска рекламы из существующего lifecycle игры.</summary>
    public static class AdsManager
    {
        public static void Initialize() => RewardedAdService.Instance.RequestInitialization();
        public static void OnEnable() => Initialize();
        public static void OnDisable() { }
    }
}