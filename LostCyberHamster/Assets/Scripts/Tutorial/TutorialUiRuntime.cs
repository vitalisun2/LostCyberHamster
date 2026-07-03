using System.Threading.Tasks;
using GameManagement;
using LostCyberHamster.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Assets.Scripts.Tutorial
{
    public static class TutorialUiRuntime
    {
        private const int _retryDelayMs = 120;
        private const int _maxCompletionChecks = 8;

        private static readonly TutorialUiInputBlocker _inputBlocker = new();

        private static bool _isActive;
        private static VisualElement _activeRoot;
        private static VisualElement _activeTarget;
        private static TutorialUiPrompt _activePrompt;
        private static ScreenEnum? _loggedPendingSurface;
        private static float _nextSurfaceWaitLogAt;
        private static TutorialUiPrompt _loggedMissingPrompt;
        private static bool _isTicking;

        public static void Activate()
        {
            if (_isActive)
            {
                return;
            }

            TutorialMetaCoordinator.SkinLessonCompleted += OnSkinLessonCompleted;
            _isActive = true;
        }

        public static void Reset()
        {
            ClearActiveSurface();
        }

        public static void Tick()
        {
            if (_isTicking)
            {
                return;
            }

            _isTicking = true;
            try
            {
                TickCore();
            }
            finally
            {
                _isTicking = false;
            }
        }

        private static void TickCore()
        {
            if (!_isActive)
            {
                return;
            }

            if (!TutorialMetaCoordinator.IsActive)
            {
                ClearActiveSurface();
                return;
            }

            TryCompletePendingSurfaceTransition();
            AttachCurrentPromptIfPossible();
        }

        private static void TryCompletePendingSurfaceTransition()
        {
            var completionSurface = TutorialMetaCoordinator.CurrentCompletionSurface;
            if (!completionSurface.HasValue)
            {
                _loggedPendingSurface = null;
                _nextSurfaceWaitLogAt = 0f;
                return;
            }

            if (_loggedPendingSurface != completionSurface || Time.unscaledTime >= _nextSurfaceWaitLogAt)
            {
                _loggedPendingSurface = completionSurface;
                _nextSurfaceWaitLogAt = Time.unscaledTime + 1f;
                DebugManager.DiagStability(
                    $"[TUTORIAL UI] waiting surface={completionSurface.Value} probe={BuildSurfaceProbe(completionSurface.Value)}");
            }

            if (!TryFindSurfaceRoot(completionSurface.Value, out _))
            {
                return;
            }

            DebugManager.DiagStability($"[TUTORIAL UI] surface root found surface={completionSurface.Value}");
            var result = TutorialMetaCoordinator.NotifySurfaceLoaded(completionSurface.Value);
            if (result.IsAccepted)
            {
                DebugManager.DiagStability($"[TUTORIAL UI] surface accepted surface={completionSurface.Value}");
                ClearActiveSurface();
            }
        }

        private static void AttachCurrentPromptIfPossible()
        {
            var prompt = TutorialMetaCoordinator.CurrentPrompt;
            if (prompt == null)
            {
                ClearActiveSurface();
                return;
            }

            if (!TryFindPromptTarget(prompt, out var root, out var target))
            {
                if (_loggedMissingPrompt != prompt)
                {
                    _loggedMissingPrompt = prompt;
                    DebugManager.DiagStability(
                        $"[TUTORIAL UI] prompt target missing surface={prompt.Surface} target={prompt.Target} " +
                        $"probe={BuildSurfaceProbe(prompt.Surface)}");
                }
                return;
            }

            if (_activeRoot == root && _activeTarget == target && _activePrompt == prompt)
            {
                return;
            }

            AttachPrompt(root, target, prompt);
        }

        private static void AttachPrompt(
            VisualElement root,
            VisualElement target,
            TutorialUiPrompt prompt)
        {
            ClearActiveSurface();
            _loggedMissingPrompt = null;

            _activeRoot = root;
            _activeTarget = target;
            _activePrompt = prompt;

            DebugManager.DiagStability(
                $"[TUTORIAL UI] prompt attached surface={prompt.Surface} target={prompt.Target} element={target.name}");
            _inputBlocker.Attach(root, target);
            root.RegisterCallback<ClickEvent>(ObserveAllowedClick, TrickleDown.TrickleDown);
            TutorialMetaOverlay.ShowFocus(root, target, prompt.Instruction, prompt.Shape);
            RunAutomationActionIfNeeded();
        }

        private static void ObserveAllowedClick(ClickEvent evt)
        {
            if (!TutorialMetaCoordinator.IsActive
                || _activeRoot == null
                || _activeTarget == null
                || !IsEventInsideTarget(evt))
            {
                return;
            }

            var action = TutorialMetaCoordinator.CurrentAction;
            if (TutorialSkinLessonSandboxActions.Handles(action))
            {
                evt.StopImmediatePropagation();
                var context = TutorialSkinLessonSandboxActions.Execute(action);
                CompleteAllowedClickAfterCurrentEventAsync(action, context);
                return;
            }

            if (TutorialMetaCoordinator.CurrentStepWaitsForSurfaceCompletion)
            {
                return;
            }

            CompleteAllowedClickAfterCurrentEventAsync(action, CreateActionContext(action));
        }

        private static async void CompleteAllowedClickAfterCurrentEventAsync(
            TutorialUiAction action,
            TutorialUiActionContext context)
        {
            await Task.Yield();
            TryCompleteAction(action, context, _maxCompletionChecks);
        }

        private static void TryCompleteAction(
            TutorialUiAction action,
            TutorialUiActionContext context,
            int checksLeft)
        {
            if (!TutorialMetaCoordinator.IsActive)
            {
                return;
            }

            var result = TutorialMetaCoordinator.Notify(action, context);
            if (result.IsCompleted)
            {
                return;
            }

            if (result.Status == TutorialUiFlowResultStatus.Advanced)
            {
                RefreshOrWaitForNextSurface();
                return;
            }

            if (result.Status == TutorialUiFlowResultStatus.Stayed && checksLeft > 0 && _activeRoot != null)
            {
                _activeRoot.schedule.Execute(() => TryCompleteAction(action, context, checksLeft - 1))
                    .ExecuteLater(_retryDelayMs);
            }
        }

        private static TutorialUiActionContext CreateActionContext(TutorialUiAction action)
        {
            return action == TutorialUiAction.SelectNextSkin
                ? TutorialUiActionContext.ForSkin(TutorialMetaCoordinator.ElectricStrikeSkinId)
                : TutorialUiActionContext.Empty;
        }

        private static void RefreshOrWaitForNextSurface()
        {
            var prompt = TutorialMetaCoordinator.CurrentPrompt;
            if (prompt == null)
            {
                ClearActiveSurface();
                return;
            }

            if (!TryFindPromptTarget(prompt, out var root, out var target))
            {
                ClearActiveSurface();
                return;
            }

            AttachPrompt(root, target, prompt);
        }

        private static bool TryFindPromptTarget(
            TutorialUiPrompt prompt,
            out VisualElement root,
            out VisualElement target)
        {
            root = null;
            target = null;

            foreach (var uiDocument in Object.FindObjectsByType<UIDocument>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                var candidateRoot = uiDocument.rootVisualElement;
                if (candidateRoot?.panel == null)
                {
                    continue;
                }

                var candidateTarget = TutorialUiTargetResolver.Resolve(candidateRoot, prompt.Target);
                if (candidateTarget?.panel == null)
                {
                    continue;
                }

                root = candidateRoot;
                target = candidateTarget;
                return true;
            }

            return false;
        }

        private static bool TryFindSurfaceRoot(ScreenEnum surface, out VisualElement root)
        {
            root = null;
            foreach (var uiDocument in Object.FindObjectsByType<UIDocument>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                var candidateRoot = uiDocument.rootVisualElement;
                if (candidateRoot?.panel == null)
                {
                    continue;
                }

                if (!ContainsSurfaceMarker(candidateRoot, surface))
                {
                    continue;
                }

                root = candidateRoot;
                return true;
            }

            return false;
        }

        private static bool ContainsSurfaceMarker(VisualElement root, ScreenEnum surface)
        {
            return surface switch
            {
                ScreenEnum.HomeScreen => TutorialUiTargetResolver.Resolve(root, TutorialUiTarget.HomeCharacterButton) != null,
                ScreenEnum.CharacterScreen => TutorialUiTargetResolver.Resolve(root, TutorialUiTarget.SkinNextButton) != null,
                ScreenEnum.WinModal => TutorialUiTargetResolver.Resolve(root, TutorialUiTarget.WinHomeButton) != null,
                _ => false
            };
        }

        private static string BuildSurfaceProbe(ScreenEnum surface)
        {
            var documents = 0;
            var panelRoots = 0;
            var markerRoots = 0;

            foreach (var uiDocument in Object.FindObjectsByType<UIDocument>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                documents++;
                var candidateRoot = uiDocument.rootVisualElement;
                if (candidateRoot?.panel == null)
                {
                    continue;
                }

                panelRoots++;
                if (ContainsSurfaceMarker(candidateRoot, surface))
                {
                    markerRoots++;
                }
            }

            return $"documents={documents} panelRoots={panelRoots} markerRoots={markerRoots}";
        }

        private static void ClearActiveSurface()
        {
            if (_activeRoot != null)
            {
                _inputBlocker.Detach();
                _activeRoot.UnregisterCallback<ClickEvent>(ObserveAllowedClick, TrickleDown.TrickleDown);
                TutorialMetaOverlay.Hide(_activeRoot);
            }

            _activeRoot = null;
            _activeTarget = null;
            _activePrompt = null;
            _loggedPendingSurface = null;
            _loggedMissingPrompt = null;
        }

        private static bool IsEventInsideTarget(EventBase evt)
        {
            return evt.target is VisualElement visualElement
                   && IsSameOrChildOfTarget(visualElement);
        }

        private static bool IsSameOrChildOfTarget(VisualElement visualElement)
        {
            while (visualElement != null)
            {
                if (visualElement == _activeTarget)
                {
                    return true;
                }

                visualElement = visualElement.parent;
            }

            return false;
        }

        private static void RunAutomationActionIfNeeded()
        {
            TutorialUiAutomationDriver.DispatchClickIfEnabled(TutorialMetaCoordinator.CurrentAction, _activeTarget);
        }

        private static void OnSkinLessonCompleted()
        {
            ClearActiveSurface();
            StartSuperHitGameplayLesson();
        }

        private static void StartSuperHitGameplayLesson()
        {
            GameDataManager.PlayerData.CurrentLevel = TutorialConstants.TutorialSuperHitLevelAddress;
            SceneManager.LoadScene(TutorialConstants.GameSceneName);
        }
    }
}
