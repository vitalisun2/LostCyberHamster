using Unity.Services.Core;
using Unity.Services.Analytics;
using UnityEngine;
using System.Threading.Tasks;
using Vues.GameCore;
using Assets.Scripts.Common.Models;
using Assets.Scripts.Legacy;

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
    }

    /// <summary>
    /// Unsubscribes from game events to prevent memory leaks.
    /// </summary>
    private static void UnsubscribeFromEvents()
    {
        GameEventsManager.OnLevelStarted -= TrackLevelStart;
        GameEventsManager.OnLevelCompleted -= TrackLevelComplete;
        GameEventsManager.OnSkinPurchased -= TrackSkinPurchased;
    }

    /// <summary>
    /// Tracks the skin purchased event.
    /// </summary>
    /// <param name="skinId">The name or ID of the skin.</param>
    private static void TrackSkinPurchased(int skinId, ResourceType resourceType, int amount)
    {
        if (!_initialized) return;

        var skinPurchasedEvent = new SkinPurchasedEvent(skinId.ToString());
        AnalyticsService.Instance.RecordEvent(skinPurchasedEvent);
    }

    /// <summary>
    /// Tracks the level start event.
    /// </summary>
    /// <param name="levelNumber">The number of the level.</param>
    private static void TrackLevelStart(LevelKey levelKey)
    {
        if (!_initialized) return;

        var levelStartEvent = new LevelStartEvent(LegacyLevelBridge.ToName(levelKey));
        AnalyticsService.Instance.RecordEvent(levelStartEvent);
    }

    /// <summary>
    /// Tracks the level completion event.
    /// </summary>
    /// <param name="levelNumber">The number of the level.</param>
    /// <param name="completionTime">The time taken to complete the level.</param>
    /// <param name="success">Whether the level was successfully completed.</param>
    private static void TrackLevelComplete(LevelKey levelKey, int stars)
    {
        if (!_initialized) return;

        var levelCompleteEvent = new LevelCompleteEvent(LegacyLevelBridge.ToName(levelKey), stars);
        AnalyticsService.Instance.RecordEvent(levelCompleteEvent);
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
