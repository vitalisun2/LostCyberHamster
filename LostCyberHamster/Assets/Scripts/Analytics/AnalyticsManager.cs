using System.Threading.Tasks;
using Assets.Scripts.System;
using Unity.Services.Analytics;
using UnityEngine;
using Vues.GameCore;

public static class AnalyticsManager
{
    private static bool _initialized = false;

    /// <summary>
    /// Инициализирует сервис аналитики после готовности Unity Gaming Services.
    /// </summary>
    public static async Task InitializeAsync()
    {
        if (AutomationRuntimePrefs.IsTestLevelAutomationRun())
            return;

        if (_initialized)
        {

            return;
        }

        try
        {
            AnalyticsService.Instance.StartDataCollection();
            _initialized = true;


            SubscribeToEvents();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error initializing Analytics: {e.Message}");
        }
    }

    /// <summary>
    /// Подписывается на игровые события для отправки аналитики.
    /// </summary>
    private static void SubscribeToEvents()
    {
        GameEventsManager.OnLevelStarted += TrackLevelStart;
        GameEventsManager.OnLevelCompleted += TrackLevelComplete;
        GameEventsManager.OnSkinPurchased += TrackSkinPurchased;
    }

    /// <summary>
    /// Отписывается от игровых событий аналитики.
    /// </summary>
    private static void UnsubscribeFromEvents()
    {
        GameEventsManager.OnLevelStarted -= TrackLevelStart;
        GameEventsManager.OnLevelCompleted -= TrackLevelComplete;
        GameEventsManager.OnSkinPurchased -= TrackSkinPurchased;
    }

    /// <summary>
    /// Отправляет событие покупки скина.
    /// </summary>
    private static void TrackSkinPurchased(int skinId, ResourceType resourceType, int amount)
    {
        if (!_initialized) return;

        var skinPurchasedEvent = new SkinPurchasedEvent(skinId.ToString());
        AnalyticsService.Instance.RecordEvent(skinPurchasedEvent);
    }

    /// <summary>
    /// Отправляет событие начала уровня.
    /// </summary>
    private static void TrackLevelStart(int levelNumber)
    {
        if (!_initialized) return;

        var levelStartEvent = new LevelStartEvent(levelNumber);
        AnalyticsService.Instance.RecordEvent(levelStartEvent);
    }

    /// <summary>
    /// Отправляет событие завершения уровня.
    /// </summary>
    private static void TrackLevelComplete(int levelNumber, int stars)
    {
        if (!_initialized) return;

        var levelCompleteEvent = new LevelCompleteEvent(levelNumber, stars);
        AnalyticsService.Instance.RecordEvent(levelCompleteEvent);
    }

    /// <summary>
    /// Восстанавливает подписки при включении менеджера.
    /// </summary>
    public static void OnEnable()
    {
        if (_initialized)
        {
            SubscribeToEvents();
        }
    }

    /// <summary>
    /// Снимает подписки при выключении менеджера.
    /// </summary>
    public static void OnDisable()
    {
        if (_initialized)
        {
            UnsubscribeFromEvents();
        }
    }
}
