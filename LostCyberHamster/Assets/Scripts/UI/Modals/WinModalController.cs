using System;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.System;
using Assets.Scripts.Tutorial;
using GameManagement.Leaderboard;
using UnityEngine;
using UnityEngine.UIElements;
using Vues.GameCore;

namespace LostCyberHamster.UI
{
    public class WinModalController : ModalController
    {
        private VisualElement _resumeButton => _modalContent.Q<VisualElement>("btn__play");

        private VisualElement _restartButton => _modalContent.Q<VisualElement>("btn__repeat");

        private VisualElement _exitButton => _modalContent.Q<VisualElement>("btn__home");

        private VisualElement _starsContainer => _modalContent.Q<VisualElement>("stars_container");

        private VisualElement _resultContainer =>
            _modalContent.Q<VisualElement>("win_result");

        private Label _runScoreLabel =>
            _modalContent.Q<Label>("win_run_score");

        private Label _recordLabel =>
            _modalContent.Q<Label>("win_record");

        private Label _submissionStatusLabel =>
            _modalContent.Q<Label>("win_submission_status");

        private Button _leaderboardButton =>
            _modalContent.Q<Button>("btn_leaderboard");

        private Label _levelContextLabel =>
            _modalContent.Q<Label>("level_context");

        private Action _actionResume;

        private Action _actionRestart;

        private Action _actionExit;

        private Action<string, string> _actionLeaderboard;
        private Action _actionGoal;
        private VisualElement _goalStrip => _modalContent.Q<VisualElement>("first-session-win-goal");
        private Label _goalLabel => _modalContent.Q<Label>("first-session-win-goal-text");
        private Button _goalButton => _modalContent.Q<Button>("btn_first-session-goal");
        private bool _primaryShowsStoryClaim;

        private GameResultModalPresentation _presentation;

        protected override ScreenEnum _modalAssetName => ScreenEnum.WinModal;

        private string _locationName;
        private string _levelName;
        private int _stars;
        private RunResultData _runResult;

        public WinModalController(UIDocument uiDocument) : base(uiDocument)
        {
        }

        protected override Task OnShowAsync()
        {
            // Разворачиваем общий modal host под полноэкранный эталон.
            _presentation?.Restore();
            _presentation = GameResultModalPresentation.Apply(_root);
            _buttonCloseModal.style.display = DisplayStyle.None;

            // Локализуем контекст уровня до показа дерева.
            _levelContextLabel.text = FormatLevelContext();

            // Показываем только заработанные звёзды из подготовленного набора.
            int visibleStars = Math.Max(0, Math.Min(_stars, 3));
            for (int i = 1; i <= 3; i++)
            {
                var star = _starsContainer.Q($"star{i}");
                if (star != null)
                {
                    star.style.display = i <= visibleStars
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                }
            }

            RenderRunResult();
            RenderGoal();
            return Task.CompletedTask;
        }

        protected override void OnSubscribeToEvents()
        {
            _presentation ??= GameResultModalPresentation.Apply(_root);
            _resumeButton?.RegisterCallback<ClickEvent>(OnClickResume);
            _restartButton?.RegisterCallback<ClickEvent>(OnClickRestart);
            _exitButton?.RegisterCallback<ClickEvent>(OnClickExit);
            _leaderboardButton?.RegisterCallback<ClickEvent>(OnClickLeaderboard);
            _goalButton?.UnregisterCallback<ClickEvent>(OnClickGoal);
            _goalButton?.RegisterCallback<ClickEvent>(OnClickGoal);

        }

        private void OnClickLeaderboard(ClickEvent evt)
        {
            if (_runResult?.IsLastLevelOfPart != true)
                return;

            AcknowledgeVisibleRecord();
            _actionLeaderboard?.Invoke(
                _runResult.LevelKey.LocationId,
                _runResult.LevelKey.PartOfDayId);
        }

        private void OnClickExit(ClickEvent evt)
        {
            AcknowledgeVisibleRecord();
            _actionExit?.Invoke();
        }


        private void OnClickRestart(ClickEvent evt)
        {
            AcknowledgeVisibleRecord();
            _actionRestart?.Invoke();
        }


        private void OnClickResume(ClickEvent evt)
        {
            AcknowledgeVisibleRecord();
            if (_primaryShowsStoryClaim) _actionGoal?.Invoke();
            else _actionResume?.Invoke();
        }

        private void OnClickGoal(ClickEvent evt)
        {
            AcknowledgeVisibleRecord();
            if (_primaryShowsStoryClaim) _actionResume?.Invoke();
            else _actionGoal?.Invoke();
        }


        protected override void OnUnsubscribeFromEvents()
        {
            _resumeButton?.UnregisterCallback<ClickEvent>(OnClickResume);
            _restartButton?.UnregisterCallback<ClickEvent>(OnClickRestart);
            _exitButton?.UnregisterCallback<ClickEvent>(OnClickExit);
            _leaderboardButton?.UnregisterCallback<ClickEvent>(OnClickLeaderboard);
            _goalButton?.UnregisterCallback<ClickEvent>(OnClickGoal);
            _presentation?.Restore();
            _presentation = null;
        }

        public void SetResumeAction(Action value)
        {
            _actionResume = value;
        }

        public void SetRestartAction(Action value)
        {
            _actionRestart = value;
        }

        public void SetExitAction(Action value)
        {
            _actionExit = value;
        }

        /// <summary>Задаёт переход к текущей цели первой сессии через общий маршрут результата.</summary>
        public void SetGoalAction(Action value) => _actionGoal = value;

        /// <summary>
        /// Задаёт переход из результата забега в выбранный рейтинг.
        /// </summary>
        public void SetLeaderboardAction(Action<string, string> value)
        {
            _actionLeaderboard = value;
        }

        public void SetParamsForInit(string locationName, string levelName, int stars)
        {
            _locationName = locationName;
            _levelName = levelName;
            _stars = stars;
        }

        /// <summary>
        /// Сохраняет новое состояние результата и обновляет открытую модалку.
        /// </summary>
        public void SetRunResult(RunResultData runResult)
        {
            // Сохраняем состояние даже до загрузки дерева модалки.
            _runResult = runResult;

            // Обновляем только уже клонированный WinModal.
            if (_resultContainer != null)
            {
                _levelContextLabel.text = FormatLevelContext();
                RenderRunResult();
                RenderGoal();
            }
        }

        private void RenderGoal()
        {
            if (_goalStrip == null) return;
            var goal = FirstSessionGoalPresenter.GetCurrent();
            _primaryShowsStoryClaim = QuestManager.StoryQuests.Any(quest =>
                quest.Id == FirstSessionGoalPresenter.MorningQuestId && quest.CanClaimReward);
            RenderPrimaryAction();
            _goalStrip.style.display = goal.HasValue ? DisplayStyle.Flex : DisplayStyle.None;
            if (!goal.HasValue) return;
            _goalLabel.text = goal.Value.Text;
            _goalLabel.tooltip = goal.Value.Detail;
            _goalButton.text = _primaryShowsStoryClaim
                ? LocalizationManager.GetLocalizedString("first_session_next_level") : goal.Value.ActionText;
        }

        private void RenderPrimaryAction()
        {
            if (_resumeButton is not Button primary) return;
            // Размер и позиция прежние; надпись Next в арте скрывается только для настоящего Claim-маршрута.
            bool claim = _primaryShowsStoryClaim;
            primary.text = claim ? LocalizationManager.GetLocalizedString("first_session_claim_action") : string.Empty;
            primary.tooltip = LocalizationManager.GetLocalizedString(claim
                ? "first_session_claim_action" : "first_session_next_level");
            primary.style.backgroundImage = claim ? new StyleBackground(StyleKeyword.None) : new StyleBackground(StyleKeyword.Null);
            primary.style.backgroundColor = claim ? new StyleColor(new Color(0.88f, 0.93f, 0.965f)) : new StyleColor(StyleKeyword.Null);
            primary.style.color = claim ? new StyleColor(new Color(0.06f, 0.16f, 0.31f)) : new StyleColor(StyleKeyword.Null);
            primary.style.fontSize = claim ? new StyleLength(26) : new StyleLength(StyleKeyword.Null);
            primary.style.unityFontStyleAndWeight = claim ? new StyleEnum<FontStyle>(FontStyle.Bold) : new StyleEnum<FontStyle>(StyleKeyword.Null);
            primary.style.unityTextAlign = TextAnchor.MiddleCenter;
            primary.style.whiteSpace = WhiteSpace.Normal;
            StyleColor border = claim ? new StyleColor(new Color(0.145f, 0.38f, 0.55f)) : new StyleColor(StyleKeyword.Null);
            primary.style.borderTopColor = primary.style.borderRightColor = primary.style.borderBottomColor = primary.style.borderLeftColor = border;
            StyleFloat borderWidth = claim ? new StyleFloat(2) : new StyleFloat(StyleKeyword.Null);
            primary.style.borderTopWidth = primary.style.borderRightWidth = primary.style.borderBottomWidth = primary.style.borderLeftWidth = borderWidth;
            StyleLength radius = claim ? new StyleLength(18) : new StyleLength(StyleKeyword.Null);
            primary.style.borderTopLeftRadius = primary.style.borderTopRightRadius = primary.style.borderBottomLeftRadius = primary.style.borderBottomRightRadius = radius;
        }

        private void AcknowledgeVisibleRecord()
        {
            // Только видимая надпись нового рекорда, а не кнопка рейтинга или скрытая модель данных.
            if (_runResult?.IsNewRecord != true ||
                _runResult.SubmissionState != RunResultSubmissionState.Submitted) return;
            var label = _recordLabel;
            if (label == null || label.panel == null || label.worldBound.width <= 0 || label.worldBound.height <= 0) return;
            for (var element = (VisualElement)label; element != null; element = element.parent)
                if (element.resolvedStyle.display == DisplayStyle.None || !element.visible) return;

            // Явный выход подтверждает receipt именно этого забега; ошибка ACK не отменяет навигацию.
            try
            {
                string runId = LevelController.Instance?.LevelData?.Hamster?.LatestLeaderboardRunId;
                var coordinator = WeeklyLeaderboardCoordinator.Instance;
                var notification = coordinator?.GetPendingRecordNotifications().FirstOrDefault(item =>
                    item.RunId == runId && item.Score == _runResult.RunScore);
                if (notification != null) coordinator.AcknowledgeRecordNotification(notification);
            }
            catch (Exception exception)
            {
                DebugManager.DiagStability($"[WeeklyLeaderboard] Win presentation ACK failed ({exception.GetType().Name}).");
            }
        }

        private void RenderRunResult()
        {
            if (_runResult == null)
            {
                _resultContainer.style.display = DisplayStyle.None;
                _leaderboardButton.style.display = DisplayStyle.None;
                return;
            }

            // Всегда показываем завершённый забег.
            _resultContainer.style.display = DisplayStyle.Flex;
            _runScoreLabel.text = FormatLocalized(
                "win_run_score",
                _runResult.RunScore.ToString("0")) + " · " + FormatLocalized("progression_win_xp",
                    LevelManager.LastCompletionExperience.Amount.ToString());

            // Вторая строка показывает один статус без наложения соседних текстов.
            var isResolved =
                _runResult.SubmissionState == RunResultSubmissionState.Submitted ||
                _runResult.SubmissionState == RunResultSubmissionState.NotRequired;
            var showLeaderboard =
                isResolved && _runResult.IsLastLevelOfPart;
            _recordLabel.style.display = isResolved && !showLeaderboard
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            if (isResolved)
            {
                _recordLabel.text = FormatLocalized(
                    _runResult.IsNewRecord
                        ? "win_new_record"
                        : "win_existing_record",
                    GetLocalizedPartName(_runResult.LevelKey.PartOfDayId),
                    _runResult.WeeklyBestRunScore.ToString("0"));
            }

            _leaderboardButton.style.display = showLeaderboard
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _leaderboardButton.SetEnabled(showLeaderboard);
            _leaderboardButton.text = FormatLocalized(
                "win_open_leaderboard",
                GetLocalizedPartName(_runResult.LevelKey.PartOfDayId));

            _submissionStatusLabel.style.display = isResolved
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            _submissionStatusLabel.text = _runResult.SubmissionState switch
            {
                RunResultSubmissionState.Pending =>
                    LocalizationManager.GetLocalizedString("win_submit_pending"),
                RunResultSubmissionState.Submitted =>
                    LocalizationManager.GetLocalizedString("win_submit_success"),
                RunResultSubmissionState.Failed =>
                    LocalizationManager.GetLocalizedString("win_submit_error"),
                RunResultSubmissionState.LocalOnly =>
                    LocalizationManager.GetLocalizedString(_runResult.LocalOnlyReason switch
                    {
                        WeeklyLocalOnlyReason.OwnerUnassigned => "win_submit_local_only_owner",
                        WeeklyLocalOnlyReason.SeasonUnknown => "win_submit_local_only_season",
                        _ => "win_submit_local_only"
                    }),
                RunResultSubmissionState.Expired =>
                    LocalizationManager.GetLocalizedString("win_submit_expired"),
                RunResultSubmissionState.Unconfirmed =>
                    LocalizationManager.GetLocalizedString("win_submit_unconfirmed"),
                _ => string.Empty
            };
        }

        private string FormatLevelContext()
        {
            // RunResult содержит стабильные localization keys.
            string locationKey = _runResult?.LevelKey.LocationId;
            string partKey = _runResult?.LevelKey.PartOfDayId;
            string location = GetLocalizedOrFallback(
                locationKey,
                _locationName);
            string part = GetLocalizedOrFallback(
                partKey,
                _levelName);

            // Собираем одну строку, чтобы части не конкурировали за ширину.
            if (string.IsNullOrWhiteSpace(location))
            {
                return (part ?? string.Empty).ToUpperInvariant();
            }

            if (string.IsNullOrWhiteSpace(part))
            {
                return location.ToUpperInvariant();
            }

            return $"{location} — {part}".ToUpperInvariant();
        }

        private static string FormatLocalized(string key, params string[] values)
        {
            // Берём шаблон текущего языка с безопасным fallback на ключ.
            var template =
                LocalizationManager.GetLocalizedString(key) ?? key;

            // Подставляем все значения без зависимости от системной культуры.
            for (var index = 0; index < values.Length; index++)
            {
                template = template.Replace(
                    $"{{{index}}}",
                    values[index] ?? string.Empty);
            }

            return template;
        }

        private static string GetLocalizedPartName(string partId)
        {
            return LocalizationManager.GetLocalizedString(
                $"leaderboard_{partId?.Trim().ToLowerInvariant()}");
        }

        private static string GetLocalizedOrFallback(
            string key,
            string fallback)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                string localized =
                    LocalizationManager.GetLocalizedString(key);
                if (!string.IsNullOrWhiteSpace(localized))
                {
                    return localized;
                }
            }

            return fallback ?? string.Empty;
        }
    }
}
