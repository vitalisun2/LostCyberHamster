using System;
using System.Threading.Tasks;
using Assets.Scripts.Online;
using Assets.Scripts.System;
using GameManagement;
using Unity.Services.Analytics;
using UnityEngine;
using UnityEngine.UnityConsent;
using Vues.GameCore;
using Vues.GameCore.ReturnActivities;

public static class AnalyticsManager
{
    private static bool _initialized = false;
    private static int _trackingSuppressionCount;
    private static bool _subscribed;
    public static bool IsInitialized => _initialized;

    internal static bool CanRecordReturnActivity => CanTrack() && !Application.isEditor && !Debug.isDebugBuild &&
        !GameDataManager.IsProgressionTestingProfile && !AutomationRuntimePrefs.IsTestLevelAutomationRun() &&
        EndUserConsent.GetConsentState().AnalyticsIntent == ConsentStatus.Granted;

    /// <summary>Передаёт событие активностей в SDK только при действующем consent и production-контексте.</summary>
    internal static void RecordReturnActivity(ReturnActivityEvent item)
    {
        if (CanRecordReturnActivity) AnalyticsService.Instance.RecordEvent(new ReturnActivityAnalyticsEvent(item));
    }

    /// <summary>Записывает только явные события первой сессии сквозь tutorial suppression, сохраняя consent.</summary>
    internal static void RecordFirstSession(string phase, string detail, int value, string sessionId,
        int sequence, double elapsedSeconds)
    {
        if (!_initialized || Application.isEditor || Debug.isDebugBuild ||
            AutomationRuntimePrefs.IsTestLevelAutomationRun() ||
            EndUserConsent.GetConsentState().AnalyticsIntent != ConsentStatus.Granted) return;

        // Обычные level/skin события tutorial по-прежнему используют CanTrack и остаются подавленными.
        var player = GameDataManager.PlayerData;
        string outcome = player?.IsTutorialCompleted != true ? "pending" : player.IsTutorialSkipped ? "skip" : "complete";
        string networkMode = GameNetworkFacade.Instance.IsForcedOffline ? "forced_offline" :
            Application.internetReachability == NetworkReachability.NotReachable ? "unreachable" : "available";
        AnalyticsService.Instance.RecordEvent(new FirstSessionEvent(phase, detail, value, sessionId, sequence,
            elapsedSeconds, GameDataManager.ProfileId ?? string.Empty, player?.CurrentLevel ?? string.Empty,
            player?.PlayerLevel ?? 1, outcome, Application.version, networkMode));
    }

    public static IDisposable SuppressTracking()
    {
        _trackingSuppressionCount++;
        return new TrackingSuppressionLease();
    }

    /// <summary>
    /// Инициализирует сервис аналитики после готовности Unity Gaming Services.
    /// </summary>
    public static Task InitializeAsync()
    {
        if (AutomationRuntimePrefs.IsTestLevelAutomationRun())
            return Task.CompletedTask;

        if (_initialized)
        {

            return Task.CompletedTask;
        }

        try
        {
            // Сохраняем действующий выбор; прежний автоматический старт применяется при первом запуске.
            var consent = EndUserConsent.GetConsentState();
            if (consent.AnalyticsIntent == ConsentStatus.Unspecified)
            {
                consent.AnalyticsIntent = ConsentStatus.Granted;
                EndUserConsent.SetConsentState(consent);
            }
            _initialized = true;


            SubscribeToEvents();
        }
        catch
        {
            _initialized = false;
            throw;
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Подписывается на игровые события для отправки аналитики.
    /// </summary>
    private static void SubscribeToEvents()
    {
        if (_subscribed) return;
        _subscribed = true;
        GameEventsManager.OnLevelStarted += TrackLevelStart;
        GameEventsManager.OnLevelCompleted += TrackLevelComplete;
        GameEventsManager.OnSkinPurchased += TrackSkinPurchased;
    }

    /// <summary>
    /// Отписывается от игровых событий аналитики.
    /// </summary>
    private static void UnsubscribeFromEvents()
    {
        _subscribed = false;
        GameEventsManager.OnLevelStarted -= TrackLevelStart;
        GameEventsManager.OnLevelCompleted -= TrackLevelComplete;
        GameEventsManager.OnSkinPurchased -= TrackSkinPurchased;
    }

    /// <summary>
    /// Отправляет событие покупки скина.
    /// </summary>
    private static void TrackSkinPurchased(int skinId, ResourceType resourceType, int amount)
    {
        if (!CanTrack()) return;

        var skinPurchasedEvent = new SkinPurchasedEvent(skinId.ToString());
        AnalyticsService.Instance.RecordEvent(skinPurchasedEvent);
    }

    /// <summary>
    /// Отправляет событие начала уровня.
    /// </summary>
    private static void TrackLevelStart(int levelNumber)
    {
        if (!CanTrack()) return;

        var levelStartEvent = new LevelStartEvent(levelNumber);
        AnalyticsService.Instance.RecordEvent(levelStartEvent);
    }

    /// <summary>
    /// Отправляет событие завершения уровня.
    /// </summary>
    private static void TrackLevelComplete(int levelNumber, int stars)
    {
        if (!CanTrack()) return;

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

    private static bool CanTrack()
    {
        return _initialized && _trackingSuppressionCount == 0;
    }

    private sealed class TrackingSuppressionLease : IDisposable
    {
        private bool _isDisposed;

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _trackingSuppressionCount = Math.Max(0, _trackingSuppressionCount - 1);
            _isDisposed = true;
        }
    }
}
