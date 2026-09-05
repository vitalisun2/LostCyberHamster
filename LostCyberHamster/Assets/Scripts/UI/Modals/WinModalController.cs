using System;
using System.Threading.Tasks;
using Assets.Scripts.GameEngine.Mechanics;
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
            return Task.CompletedTask;
        }

        protected override void OnSubscribeToEvents()
        {
            _presentation ??= GameResultModalPresentation.Apply(_root);
            _resumeButton?.RegisterCallback<ClickEvent>(OnClickResume);
            _restartButton?.RegisterCallback<ClickEvent>(OnClickRestart);
            _exitButton?.RegisterCallback<ClickEvent>(OnClickExit);
            _leaderboardButton?.RegisterCallback<ClickEvent>(OnClickLeaderboard);

        }

        private void OnClickLeaderboard(ClickEvent evt)
        {
            if (_runResult?.IsLastLevelOfPart != true)
                return;

            _actionLeaderboard?.Invoke(
                _runResult.LevelKey.LocationId,
                _runResult.LevelKey.PartOfDayId);
        }

        private void OnClickExit(ClickEvent evt)
        {
            _actionExit?.Invoke();
        }


        private void OnClickRestart(ClickEvent evt)
        {
            _actionRestart?.Invoke();
        }


        private void OnClickResume(ClickEvent evt)
        {
            _actionResume?.Invoke();
        }


        protected override void OnUnsubscribeFromEvents()
        {
            _resumeButton?.UnregisterCallback<ClickEvent>(OnClickResume);
            _restartButton?.UnregisterCallback<ClickEvent>(OnClickRestart);
            _exitButton?.UnregisterCallback<ClickEvent>(OnClickExit);
            _leaderboardButton?.UnregisterCallback<ClickEvent>(OnClickLeaderboard);
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
                _runResult.RunScore.ToString("0"));

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
