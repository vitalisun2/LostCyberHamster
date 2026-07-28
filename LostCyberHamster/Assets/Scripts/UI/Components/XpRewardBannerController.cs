using System;
using System.Threading;
using System.Threading.Tasks;
using GameManagement.Progress;
using UnityEngine;
using UnityEngine.UIElements;

namespace LostCyberHamster.UI
{
    /// <summary>
    /// Показывает временный XP-баннер поверх результата победного забега.
    /// </summary>
    public sealed class XpRewardBannerController : IDisposable
    {
        private const int SlideDurationMilliseconds = 250;
        private const int FillDurationMilliseconds = 900;
        private const int EmptyRewardDurationMilliseconds = 600;
        private const int HoldDurationMilliseconds = 150;

        private const string VisibleClass =
            "xp-reward-banner--visible";

        private readonly VisualElement _overlay;
        private readonly VisualElement _banner;
        private readonly Label _levelLabel;
        private readonly ProgressBar _experienceProgress;
        private readonly Label _experienceLabel;
        private readonly Label _rewardLabel;
        private readonly TaskCompletionSource<bool> _completionSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenSource _cancellationSource = new();
        private bool _isStarted;
        private bool _isDisposed;

        public XpRewardBannerController(UIDocument uiDocument)
        {
            if (uiDocument == null)
            {
                throw new ArgumentNullException(nameof(uiDocument));
            }

            VisualElement root = uiDocument.rootVisualElement;
            _overlay = RequireElement<VisualElement>(
                root,
                "xp-reward-overlay");
            _banner = RequireElement<VisualElement>(
                root,
                "xp-reward-banner");
            _levelLabel = RequireElement<Label>(
                root,
                "xp-reward-level");
            _experienceProgress = RequireElement<ProgressBar>(
                root,
                "xp-reward-progress");
            _experienceLabel = RequireElement<Label>(
                root,
                "xp-reward-value");
            _rewardLabel = RequireElement<Label>(
                root,
                "xp-reward-amount");

            // Overlay только показывает награду и пропускает ввод к WinModal.
            _overlay.pickingMode = PickingMode.Ignore;
            _overlay.Query<VisualElement>().ForEach(
                element => element.pickingMode = PickingMode.Ignore);
            Hide();
        }

        /// <summary>
        /// Возвращает задачу, завершённую после ухода XP-баннера.
        /// </summary>
        public Task WaitForCompletionAsync()
        {
            return _completionSource.Task;
        }

        /// <summary>
        /// Один раз показывает итог забега и завершает общий presentation gate.
        /// </summary>
        public Task ShowAsync(RunExperienceResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (_isStarted)
            {
                return _completionSource.Task;
            }

            _isStarted = true;
            _ = PlayAsync(result, _cancellationSource.Token);
            return _completionSource.Task;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _cancellationSource.Cancel();
            Hide();
            _completionSource.TrySetResult(true);
            _cancellationSource.Dispose();
        }

        private async Task PlayAsync(
            RunExperienceResult result,
            CancellationToken cancellationToken)
        {
            try
            {
                // Показываем начальное состояние и выдвигаем overlay сверху.
                RenderProgress(
                    result.PlayerLevelBefore,
                    result.ExperiencePointsBefore);
                _rewardLabel.text = $"+{result.TotalExperience} XP";
                _rewardLabel.style.opacity = 1f;
                _overlay.style.display = DisplayStyle.Flex;
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
                _banner.AddToClassList(VisibleClass);
                await Task.Delay(
                    SlideDurationMilliseconds,
                    cancellationToken);

                // Заполняем все пройденные Level и переносим остаток.
                await AnimateExperienceAsync(
                    result,
                    cancellationToken);
                await Task.Delay(
                    HoldDurationMilliseconds,
                    cancellationToken);

                // Уводим завершённый баннер вверх.
                _banner.RemoveFromClassList(VisibleClass);
                await Task.Delay(
                    SlideDurationMilliseconds,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                Hide();
                _completionSource.TrySetResult(true);
            }
        }

        private async Task AnimateExperienceAsync(
            RunExperienceResult result,
            CancellationToken cancellationToken)
        {
            if (result.TotalExperience == 0)
            {
                await AnimateSegmentAsync(
                    result.PlayerLevelBefore,
                    result.ExperiencePointsBefore,
                    result.ExperiencePointsBefore,
                    0,
                    0,
                    EmptyRewardDurationMilliseconds,
                    cancellationToken);
                return;
            }

            int currentLevel = result.PlayerLevelBefore;
            int currentExperience = result.ExperiencePointsBefore;
            int remainingExperience = result.TotalExperience;
            int animatedExperience = 0;

            while (remainingExperience > 0)
            {
                int experienceToThreshold =
                    PlayerExperienceService.PlayerLevelThreshold -
                    currentExperience;
                int segmentExperience = Math.Min(
                    remainingExperience,
                    experienceToThreshold);
                int segmentDuration = Mathf.Max(
                    1,
                    Mathf.RoundToInt(
                        FillDurationMilliseconds *
                        (segmentExperience /
                         (float)result.TotalExperience)));

                await AnimateSegmentAsync(
                    currentLevel,
                    currentExperience,
                    currentExperience + segmentExperience,
                    animatedExperience,
                    result.TotalExperience,
                    segmentDuration,
                    cancellationToken);

                animatedExperience += segmentExperience;
                remainingExperience -= segmentExperience;
                currentExperience += segmentExperience;
                if (currentExperience >=
                    PlayerExperienceService.PlayerLevelThreshold)
                {
                    currentLevel++;
                    currentExperience = 0;
                    RenderProgress(currentLevel, currentExperience);
                }
            }

            RenderProgress(
                result.PlayerLevelAfter,
                result.ExperiencePointsAfter);
            _rewardLabel.style.opacity = 0f;
        }

        private async Task AnimateSegmentAsync(
            int playerLevel,
            int experienceFrom,
            int experienceTo,
            int animatedExperienceBefore,
            int totalExperience,
            int durationMilliseconds,
            CancellationToken cancellationToken)
        {
            float startedAt = Time.realtimeSinceStartup;
            float durationSeconds = durationMilliseconds / 1000f;
            float progress = 0f;

            while (progress < 1f)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress = Mathf.Clamp01(
                    (Time.realtimeSinceStartup - startedAt) /
                    durationSeconds);
                int displayedExperience = Mathf.RoundToInt(
                    Mathf.Lerp(
                        experienceFrom,
                        experienceTo,
                        progress));
                RenderProgress(playerLevel, displayedExperience);

                float rewardProgress = totalExperience == 0
                    ? progress
                    : (animatedExperienceBefore +
                       (experienceTo - experienceFrom) * progress) /
                      totalExperience;
                _rewardLabel.style.opacity =
                    1f - Mathf.Clamp01(rewardProgress);
                await Task.Yield();
            }
        }

        private void RenderProgress(
            int playerLevel,
            int experiencePoints)
        {
            _levelLabel.text = $"LEVEL {playerLevel}";
            _experienceProgress.lowValue = 0;
            _experienceProgress.highValue =
                PlayerExperienceService.PlayerLevelThreshold;
            _experienceProgress.value = experiencePoints;
            _experienceProgress.title = string.Empty;
            _experienceLabel.text =
                $"{experiencePoints}/" +
                $"{PlayerExperienceService.PlayerLevelThreshold} XP";
        }

        private void Hide()
        {
            _banner.RemoveFromClassList(VisibleClass);
            _overlay.style.display = DisplayStyle.None;
        }

        private static T RequireElement<T>(
            VisualElement root,
            string name)
            where T : VisualElement
        {
            T element = root.Q<T>(name);
            if (element == null)
            {
                throw new InvalidOperationException(
                    $"UI element '{name}' was not found.");
            }

            return element;
        }
    }
}
