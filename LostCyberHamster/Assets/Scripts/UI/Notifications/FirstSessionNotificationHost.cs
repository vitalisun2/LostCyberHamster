using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Tutorial;
using GameManagement;
using GameManagement.Leaderboard;
using UnityEngine;
using UnityEngine.UIElements;
using Vues.GameCore;
using Vues.GameCore.Quests;

namespace LostCyberHamster.UI
{
    /// <summary>Связывает уведомления первой сессии с подтверждёнными владельцами квестов и рекордов.</summary>
    public sealed class FirstSessionNotificationHost : IDisposable
    {
        private readonly UIManager _ui;
        private readonly VisualElement _root;
        private readonly Hamster _hamster;
        private readonly NotificationCoordinator _notifications;
        private readonly List<QuestAttemptPreview> _questCandidates = new();
        private readonly HashSet<string> _seenQuestInstances = new();
        private readonly List<NotificationMessage> _previewMessages = new();
        private readonly List<NotificationMessage> _recordMessages = new();
        private readonly List<Rect> _hudRects = new(8);
        private readonly VisualElement[] _hudElements = new VisualElement[8];
        private IReadOnlyList<WeeklyRecordNotification> _recordSnapshot = Array.Empty<WeeklyRecordNotification>();
        private WeeklyLeaderboardCoordinator _weekly;
        private WeeklyRecordPreview _seenRecordPreview;
        private VisualElement _hudRoot;
        private string _profileId;
        private long _generation;
        private string _questAttemptId;
        private double _questBatchStarted;
        private double _nextRecordRefresh;
        private bool _recordsDirty = true;
        private bool _disposed;

        public bool IsPresenting => !_disposed && _notifications.IsPresenting;

        public FirstSessionNotificationHost(UIManager ui, VisualElement root, Hamster hamster = null)
        {
            _ui = ui ?? throw new ArgumentNullException(nameof(ui));
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _hamster = hamster;
            _notifications = new NotificationCoordinator(root);
            _profileId = GameDataManager.ProfileId;
            _generation = GameDataManager.Generation;
            QuestManager.AttemptPreviewChanged += OnQuestPreviewChanged;
            QuestManager.AttemptQuestConditionReached += OnQuestConditionReached;
            if (_hamster != null) _hamster.RecordPreviewed += OnRecordPreviewed;
            BindWeekly();
            OnQuestPreviewChanged(QuestManager.CurrentAttemptPreview);
            if (_hamster != null && _hamster.LatestRecordPreview != null)
                OnRecordPreviewed(_hamster.LatestRecordPreview);
        }

        /// <summary>Показывает только разрешённые сообщения; блокировка не подтверждает прочтение.</summary>
        public void Tick(bool gameplay, bool blocked)
        {
            if (_disposed) return;
            double now = Time.realtimeSinceStartupAsDouble;
            SynchronizeProfile();
            BindWeekly();
            PrepareQuestBatch(now);
            RetryPreviews();

            // Подтверждения рекордов показывает меню; видимый Win подтверждает собственный текст отдельно.
            if (_hamster == null)
            {
                if (_recordsDirty || now >= _nextRecordRefresh) RefreshRecords(now);
                foreach (var message in _recordMessages)
                    if (_notifications.Enqueue(message)) FirstSessionTelemetry.Record("notification_queued", message.Key);
            }
            UpdateHudExclusions(gameplay);
            bool hide = blocked || _ui.CurrentModal.HasValue || _ui.HasPriorityPresentation ||
                        (_hamster != null && (!gameplay || !GameplayNotificationsEnabled));
            _notifications.Tick(gameplay, hide);
        }

        private static bool GameplayNotificationsEnabled =>
            GameDataManager.PlayerData != null && GameDataManager.PlayerData.EnableGameplayNotifications;

        private void SynchronizeProfile()
        {
            if (_profileId == GameDataManager.ProfileId && _generation == GameDataManager.Generation) return;
            _profileId = GameDataManager.ProfileId;
            _generation = GameDataManager.Generation;
            _questCandidates.Clear();
            _seenQuestInstances.Clear();
            _previewMessages.Clear();
            _recordMessages.Clear();
            _recordSnapshot = Array.Empty<WeeklyRecordNotification>();
            _seenRecordPreview = null;
            _recordsDirty = true;
        }

        private void OnQuestPreviewChanged(QuestAttemptPreviewSnapshot snapshot)
        {
            if (_disposed) return;
            if (_questAttemptId != snapshot.AttemptId)
            {
                _questAttemptId = snapshot.AttemptId;
                _questCandidates.Clear();
                _seenQuestInstances.Clear();
            }
            // Временная перепривязка/rollback может выдать пустой снимок внутри того же кадра.
            // Актуальность batch проверяется перед постановкой; дедупликация переживает восстановление.
            foreach (var preview in snapshot.Quests)
                if (preview.IsConditionReached) OnQuestConditionReached(preview);
        }

        private void OnQuestConditionReached(QuestAttemptPreview preview)
        {
            if (_disposed || _hamster == null || !GameplayNotificationsEnabled ||
                !QuestManager.IsCurrentAttemptPreview(preview) || !_seenQuestInstances.Add(preview.InstanceId)) return;
            if (_questCandidates.Count == 0) _questBatchStarted = Time.realtimeSinceStartupAsDouble;
            _questCandidates.Add(preview);
            FirstSessionTelemetry.Record("quest_preview", preview.InstanceId, preview.ProjectedProgress);
        }

        private void PrepareQuestBatch(double now)
        {
            if (_questCandidates.Count == 0 || now - _questBatchStarted < 1) return;
            var candidates = _questCandidates.Where(QuestManager.IsCurrentAttemptPreview).ToArray();
            double expiresAt = _questBatchStarted + 10;
            _questCandidates.Clear();
            if (candidates.Length == 0 || now >= expiresAt || !GameplayNotificationsEnabled) return;
            string title = candidates.Length == 1
                ? Format("notification_quest_preview_one", LocalizeQuest(candidates[0]))
                : Format("notification_quest_preview_many", candidates.Length);
            var first = candidates[0];
            string key = "quest-preview:" + string.Join("|", candidates.Select(item => item.InstanceId));
            if (candidates.Length > 1) FirstSessionTelemetry.Record("notification_merged", "quest", candidates.Length);
            _previewMessages.Add(new NotificationMessage(
                key, title,
                Localize("notification_quest_preview_detail"), first.ProfileId, first.AttemptId,
                priority: 20, durationSeconds: 4, allowedDuringGameplay: true, isProvisional: true,
                isValid: () => !_disposed && GameplayNotificationsEnabled &&
                    Time.realtimeSinceStartupAsDouble < expiresAt &&
                    candidates.All(QuestManager.IsCurrentAttemptPreview),
                onShown: () => FirstSessionTelemetry.Record("notification_shown", key),
                onAcknowledged: () => FirstSessionTelemetry.Record("notification_acknowledged", key)));
        }

        private void OnRecordPreviewed(WeeklyRecordPreview preview)
        {
            if (_disposed || preview == null || ReferenceEquals(_seenRecordPreview, preview) ||
                _hamster == null || !GameplayNotificationsEnabled) return;
            _seenRecordPreview = preview;
            FirstSessionTelemetry.Record("record_preview", preview.Context.LeaderboardId, preview.Score);
            double expiresAt = Time.realtimeSinceStartupAsDouble + 10;
            _previewMessages.Add(new NotificationMessage("record-preview", Localize("notification_record_preview"),
                Localize(preview.IsLocalOnly ? "notification_record_local_detail" : "notification_record_preview_detail"),
                preview.Context.ProfileId, QuestManager.CurrentAttemptPreview.AttemptId,
                priority: 10, durationSeconds: 3, allowedDuringGameplay: true, isProvisional: true,
                isValid: () => !_disposed && GameplayNotificationsEnabled && _hamster != null &&
                    Time.realtimeSinceStartupAsDouble < expiresAt &&
                    ReferenceEquals(_hamster.LatestRecordPreview, preview) && _hamster.LatestRunResult == null &&
                    GameDataManager.ProfileId == preview.Context.ProfileId &&
                    GameDataManager.Generation == preview.Context.Generation &&
                    GameDataManager.OwnerPlayerId == preview.Context.OwnerPlayerId,
                onShown: () => FirstSessionTelemetry.Record("notification_shown", "record-preview"),
                onAcknowledged: () => FirstSessionTelemetry.Record("notification_acknowledged", "record-preview")));
        }

        private void RetryPreviews()
        {
            for (int i = _previewMessages.Count - 1; i >= 0; i--)
            {
                var message = _previewMessages[i];
                if (!message.IsValid())
                {
                    _previewMessages.RemoveAt(i);
                    continue;
                }
                if (_notifications.Enqueue(message))
                {
                    FirstSessionTelemetry.Record("notification_queued", message.Key);
                    _previewMessages.RemoveAt(i);
                }
            }
        }

        private void BindWeekly()
        {
            if (ReferenceEquals(_weekly, WeeklyLeaderboardCoordinator.Instance)) return;
            if (_weekly != null) _weekly.RecordsChanged -= OnRecordsChanged;
            _weekly = WeeklyLeaderboardCoordinator.Instance;
            if (_weekly != null) _weekly.RecordsChanged += OnRecordsChanged;
            _recordsDirty = true;
        }

        private void OnRecordsChanged() => _recordsDirty = true;

        private void RefreshRecords(double now)
        {
            _recordsDirty = false;
            _nextRecordRefresh = now + 1;
            var next = _weekly?.GetPendingRecordNotifications() ?? Array.Empty<WeeklyRecordNotification>();
            if (_recordSnapshot.Count == next.Count &&
                _recordSnapshot.Zip(next, (a, b) => a.NotificationId == b.NotificationId).All(equal => equal)) return;
            _recordSnapshot = next;
            _recordMessages.Clear();

            // Несколько подтверждений одной доски/недели читаются вместе; XP берётся из receipts.
            foreach (var group in next.GroupBy(item => (item.LeaderboardId, item.VersionId)))
            {
                var records = group.ToArray();
                string title = records.Length > 1
                    ? Format("notification_records_confirmed", records.Length)
                    : Format(records[0].IsFirstEntry ? "notification_record_first" : "notification_record_confirmed", records[0].Score);
                string key = "record-confirmed:" + string.Join("|", records.Select(item => item.NotificationId));
                if (records.Length > 1) FirstSessionTelemetry.Record("notification_merged", "record", records.Length);
                _recordMessages.Add(new NotificationMessage(
                    key, title,
                    Format("notification_record_reward", records.Sum(item => item.AwardedExperience)),
                    records[0].ProfileId, priority: 40, durationSeconds: 4,
                    isValid: () => !_disposed && GameDataManager.ProfileId == records[0].ProfileId &&
                        GameDataManager.OwnerPlayerId == records[0].OwnerPlayerId &&
                        records.All(record => _recordSnapshot.Any(item => item.NotificationId == record.NotificationId)),
                    onShown: () => FirstSessionTelemetry.Record("notification_shown", key),
                    onAcknowledged: () =>
                    {
                        AcknowledgeRecords(records);
                        FirstSessionTelemetry.Record("notification_acknowledged", key);
                    }));
            }
        }

        private void AcknowledgeRecords(IReadOnlyList<WeeklyRecordNotification> records)
        {
            // При частичной ошибке оставшиеся receipts вернутся после обновления; выплаты не меняются.
            _recordsDirty = true;
            foreach (var record in records) _weekly?.AcknowledgeRecordNotification(record);
        }

        private void UpdateHudExclusions(bool gameplay)
        {
            _hudRects.Clear();
            if (gameplay)
            {
                if (_hudRoot == null || _hudRoot.panel == null)
                {
                    _hudRoot = _root.Q<VisualElement>("gamescreen");
                    _hudElements[0] = _hudRoot?.Q<Energybar>();
                    _hudElements[1] = _hudRoot?.Q<Healthbar>();
                    _hudElements[2] = _hudRoot?.Q<Label>("run-score");
                    _hudElements[3] = _hudRoot?.Q<Button>("btn_pause");
                    _hudElements[4] = _hudRoot?.Q<Button>("btn_buy_energy");
                    _hudElements[5] = _hudRoot?.Q<Button>("btn_buy_ulta");
                    _hudElements[6] = _hudRoot?.Q<Button>("btn_ultra");
                    _hudElements[7] = _hudRoot?.Q<Button>("btn_jump");
                }
                foreach (var element in _hudElements)
                {
                    if (element == null || element.resolvedStyle.display == DisplayStyle.None || !element.visible) continue;
                    Rect rect = element.worldBound;
                    if (rect.width <= 0 || rect.height <= 0) continue;
                    _hudRects.Add(Rect.MinMaxRect(rect.xMin - 4, rect.yMin - 4, rect.xMax + 4, rect.yMax + 4));
                }
            }
            _notifications.SetExcludedRects(_hudRects);
        }

        private static string Localize(string key) => LocalizationManager.GetLocalizedString(key);

        private static string Format(string key, params object[] values) => string.Format(Localize(key), values);

        private static string LocalizeQuest(QuestAttemptPreview preview) => string.Format(
            Localize(preview.TitleLocalizationKey), preview.TitleLocalizationArguments.Cast<object>().ToArray());

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            QuestManager.AttemptPreviewChanged -= OnQuestPreviewChanged;
            QuestManager.AttemptQuestConditionReached -= OnQuestConditionReached;
            if (_hamster != null) _hamster.RecordPreviewed -= OnRecordPreviewed;
            if (_weekly != null) _weekly.RecordsChanged -= OnRecordsChanged;
            _notifications.Dispose();
            _questCandidates.Clear();
            _previewMessages.Clear();
            _recordMessages.Clear();
        }
    }
}
