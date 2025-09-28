using Assets.Scripts.System.FeatureFlags;
using System.Threading.Tasks;
using Unity.Services.Analytics;
using Unity.Services.Core;
using UnityEngine;
using Vues.GameCore;

public static class AnalyticsManager
{
    private static bool _initialized = false;

    /// <summary>
    /// Initializes the analytics service.
    /// </summary>
    public static async Task InitializeAsync()
    {
        if (_initialized)
        {
            Debug.Log("Analytics is already initialized.");
            return;
        }

        try
        {
            await UnityServices.InitializeAsync();
            AnalyticsService.Instance.StartDataCollection();
            _initialized = true;
            Debug.Log("Analytics initialized successfully.");

            SubscribeToEvents();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error initializing Analytics: {e.Message}");
        }
    }

    /// <summary>
    /// Subscribes to game events for analytics tracking.
    /// </summary>
    private static void SubscribeToEvents()
    {
        GameEventsManager.OnLevelStarted += TrackLevelStart;
        GameEventsManager.OnLevelCompleted += TrackLevelComplete;
        GameEventsManager.OnSkinPurchased += TrackSkinPurchased;
        DayPartLevelsFeature.OnFeatureChanged += TrackFeatureFlagChanged;
    }

    /// <summary>
    /// Unsubscribes from game events to prevent memory leaks.
    /// </summary>
    private static void UnsubscribeFromEvents()
    {
        GameEventsManager.OnLevelStarted -= TrackLevelStart;
        GameEventsManager.OnLevelCompleted -= TrackLevelComplete;
        GameEventsManager.OnSkinPurchased -= TrackSkinPurchased;
        DayPartLevelsFeature.OnFeatureChanged -= TrackFeatureFlagChanged;
    }

    /// <summary>
    /// Tracks the skin purchased event.
    /// </summary>
    private static void TrackSkinPurchased(int skinId, ResourceType resourceType, int amount)
    {
        if (!_initialized) return;

        var skinPurchasedEvent = new SkinPurchasedEvent(skinId.ToString());
        AnalyticsService.Instance.RecordEvent(skinPurchasedEvent);
    }

    /// <summary>
    /// Tracks the level start event.
    /// </summary>
    private static void TrackLevelStart(int levelNumber)
    {
        if (!_initialized) return;

        var levelStartEvent = new LevelStartEvent(levelNumber);
        AnalyticsService.Instance.RecordEvent(levelStartEvent);
    }

    /// <summary>
    /// Tracks the level completion event.
    /// </summary>
    private static void TrackLevelComplete(int levelNumber, int stars)
    {
        if (!_initialized) return;

        var levelCompleteEvent = new LevelCompleteEvent(levelNumber, stars);
        AnalyticsService.Instance.RecordEvent(levelCompleteEvent);
    }

    private static void TrackFeatureFlagChanged(bool enabled)
    {
        if (!_initialized) return;

        var featureEvent = new FeatureFlagChangedEvent("day_part_levels", enabled);
        AnalyticsService.Instance.RecordEvent(featureEvent);
        Debug.Log($"[Analytics] Recorded feature flag change: day_part_levels={(enabled ? "ENABLED" : "DISABLED")}");
    }

    /// <summary>
    /// Call this method when the manager is enabled.
    /// </summary>
    public static void OnEnable()
    {
        if (_initialized)
        {
            SubscribeToEvents();
        }
    }

    /// <summary>
    /// Call this method when the manager is disabled.
    /// </summary>
    public static void OnDisable()
    {
        if (_initialized)
        {
            UnsubscribeFromEvents();
        }
    }
}
