namespace UiGallery
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using LostCyberHamster.UI;
    using Newtonsoft.Json;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using UnityEngine.UIElements;

    [Serializable]
    public sealed class InteractionCandidate
    {
        public string key, shot, caseId, scope, kind, name, type;
        public int ordinal;
        public bool enabled;
    }

    [Serializable]
    public sealed class InteractionAction
    {
        public string key, shot, kind, name, status, error;
        public string screenBefore, screenImmediatelyAfter, screenAfter;
        public string modalBefore, modalAfter, sceneBefore, sceneAfter;
        public int pointerDown, pointerUp, clicks, changes;
        public bool blockedAfter, nextInputReached, externalSideEffectGuarded;
    }

    [Serializable]
    public sealed class DiscoveryFailure
    {
        public string shot, error;
    }

    [Serializable]
    public sealed class InteractionRunResult
    {
        public string phase = "starting", current, error, cleanupError;
        public bool done, restored, success;
        public List<InteractionCandidate> candidates = new List<InteractionCandidate>();
        public List<InteractionAction> actions = new List<InteractionAction>();
        public List<DiscoveryFailure> discoveryFailures = new List<DiscoveryFailure>();
    }

    public static class InteractionRunner
    {
        const string PlanPath = __PLAN_PATH__;
        const string OutputPath = __OUTPUT_PATH__;
        const string TaskKey = __TASK_KEY__;
        static InteractionRunResult result;
        static Context context;
        static IProjectAdapter adapter;
        static bool cleaned;
        static int unsavedProgress;

        static void Save()
        {
            unsavedProgress = 0;
            File.WriteAllText(
                Path.Combine(OutputPath, "interaction-result.json"),
                JsonConvert.SerializeObject(result, Formatting.Indented));
        }

        static void SaveProgress(bool force = false)
        {
            if (force || ++unsavedProgress >= 8)
                Save();
        }

        static void CheckCancellation()
        {
            if (!Application.isPlaying ||
                object.Equals(AppDomain.CurrentDomain.GetData(TaskKey + ":cancel"), true))
                throw new OperationCanceledException("Interaction test cancelled");
        }

        public static string Start()
        {
            AppDomain.CurrentDomain.SetData(TaskKey, Run());
            return "started";
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
                Cleanup();
        }

        static void Cleanup()
        {
            if (cleaned)
                return;
            cleaned = true;
            result.phase = "restoring";
            Save();
            try
            {
                context?.ResetViews();
            }
            catch (Exception exception)
            {
                result.cleanupError = exception.ToString();
            }

            try
            {
                adapter?.Restore(context);
                result.restored = adapter == null || adapter.RestorationVerified;
                if (!result.restored)
                    throw new InvalidOperationException("Adapter restoration verification failed");
            }
            catch (Exception exception)
            {
                result.cleanupError = (result.cleanupError ?? "") + "\n" + exception;
            }

            EditorApplication.playModeStateChanged -= OnPlayMode;
            result.done = true;
            result.success = result.error == null && result.cleanupError == null &&
                             result.discoveryFailures.Count == 0 &&
                             result.actions.All(action => action.status != "failed");
            result.phase = result.success ? "complete" : "failed";
            Save();
        }

        static async Task Run()
        {
            result = new InteractionRunResult();
            Save();
            try
            {
                CheckCancellation();
                var plan = JsonConvert.DeserializeObject<Plan>(File.ReadAllText(PlanPath));
                if (plan?.shots == null || plan.shots.Length == 0)
                    throw new ArgumentException("Interaction plan is empty");
                context = new Context();
                adapter = new GalleryProjectAdapter();
                EditorApplication.playModeStateChanged += OnPlayMode;
                await adapter.Begin(context);
                CheckCancellation();
                await DiscoverAndExercise(plan);
            }
            catch (OperationCanceledException exception)
            {
                result.error = exception.ToString();
            }
            catch (Exception exception)
            {
                result.error = exception.ToString();
            }
            finally
            {
                Cleanup();
            }
        }

        static async Task Prepare(Shot shot)
        {
            context.ResetViews();
            await adapter.Reset(context);
            CheckCancellation();
            await adapter.Prepare(context, shot);
            CheckCancellation();
            await FastSettle(shot);
            CheckCancellation();
        }

        static async Task FastSettle(Shot shot)
        {
            string previous = null;
            int stable = 0;
            for (int attempt = 0; attempt < 75; attempt++)
            {
                CheckCancellation();
                var elements = (shot.expected ?? Array.Empty<string>())
                    .Select(name => context.Root.Q(name))
                    .ToArray();
                if (elements.All(context.Visible))
                {
                    var actionables = context.Root.Query<VisualElement>().ToList()
                        .Where(context.Visible)
                        .Where(IsActionable)
                        .ToList();
                    string key = string.Join("|", elements.Select(element =>
                        $"{element.worldBound.x:F1},{element.worldBound.y:F1}," +
                        $"{element.worldBound.width:F1},{element.worldBound.height:F1}")) +
                        "#" + string.Join("|", actionables.Select(element =>
                            $"{element.name}:{element.GetType().Name}:" +
                            $"{element.enabledInHierarchy}:" +
                            $"{element.worldBound.x:F1},{element.worldBound.y:F1}," +
                            $"{element.worldBound.width:F1},{element.worldBound.height:F1}"));
                    stable = key == previous ? stable + 1 : 0;
                    previous = key;
                    if (stable >= 4)
                        return;
                }
                else
                {
                    stable = 0;
                    previous = null;
                }
                await Task.Delay(20);
            }
            throw new InvalidOperationException(
                "UI missing or unstable: " + string.Join(", ", shot.expected ?? Array.Empty<string>()));
        }

        static List<InteractionCandidate> DiscoverCandidates(
            Shot shot,
            Dictionary<string, InteractionCandidate> unique)
        {
            var discovered = new List<InteractionCandidate>();
            var elements = context.Root.Query<VisualElement>().ToList()
                .Where(context.Visible)
                .Where(IsActionable)
                .Where(IsHittable)
                .ToList();
            var ordinals = new Dictionary<string, int>();
            foreach (var element in elements)
            {
                string kind = Kind(element);
                string name = element.name ?? "";
                string ordinalKey = kind + "|" + name + "|" + element.GetType().Name;
                ordinals.TryGetValue(ordinalKey, out int ordinal);
                ordinals[ordinalKey] = ordinal + 1;
                string caseId = shot.id.Split(new[] { "--" }, StringSplitOptions.None)[0];
                string scope = CurrentScreen() + "/" + (CurrentModal() ?? "none");
                string semanticState = name == "return-action" ? shot.id : "";
                string key = string.Join("|", scope, kind, name, element.GetType().Name,
                    ordinal.ToString(), element.enabledInHierarchy.ToString(), semanticState);
                if (unique.ContainsKey(key))
                    continue;
                var candidate = new InteractionCandidate
                {
                    key = key,
                    shot = shot.id,
                    caseId = caseId,
                    scope = scope,
                    kind = kind,
                    name = name,
                    type = element.GetType().Name,
                    ordinal = ordinal,
                    enabled = element.enabledInHierarchy
                };
                unique[key] = candidate;
                result.candidates.Add(candidate);
                discovered.Add(candidate);
            }
            return discovered;
        }

        static async Task DiscoverAndExercise(Plan plan)
        {
            result.phase = "interacting";
            var unique = new Dictionary<string, InteractionCandidate>();
            foreach (var shot in plan.shots)
            {
                CheckCancellation();
                result.current = shot.id;
                SaveProgress();
                List<InteractionCandidate> candidates;
                try
                {
                    await Prepare(shot);
                    candidates = DiscoverCandidates(shot, unique);
                }
                catch (Exception exception)
                {
                    result.discoveryFailures.Add(new DiscoveryFailure
                    {
                        shot = shot.id,
                        error = exception.ToString()
                    });
                    Save();
                    continue;
                }

                candidates = candidates
                    .OrderBy(candidate => candidate.enabled &&
                                          !IsExternalOrSceneAction(candidate) ? 1 : 0)
                    .ToList();
                bool stateIsPrepared = true;
                foreach (var candidate in candidates)
                {
                    CheckCancellation();
                    result.current = candidate.shot + " :: " +
                                     (string.IsNullOrEmpty(candidate.name) ? candidate.type : candidate.name);
                    var action = new InteractionAction
                    {
                        key = candidate.key,
                        shot = candidate.shot,
                        kind = candidate.kind,
                        name = candidate.name
                    };
                    try
                    {
                        if (!stateIsPrepared)
                            await Prepare(shot);
                        var element = Resolve(candidate);
                        if (element == null ||
                            element.enabledInHierarchy != candidate.enabled)
                        {
                            await Prepare(shot);
                            element = Resolve(candidate);
                        }
                        if (element == null)
                            throw new InvalidOperationException(
                                "Action element could not be resolved after one clean retry");
                        if (element.enabledInHierarchy != candidate.enabled)
                            throw new InvalidOperationException(
                                "Action enabled state drifted after one clean retry");
                        await Execute(candidate, element, action);
                    }
                    catch (Exception exception)
                    {
                        action.status = "failed";
                        action.error = exception.ToString();
                        try
                        {
                            await WaitForInputReady(400);
                        }
                        catch
                        {
                        }
                    }
                    result.actions.Add(action);
                    stateIsPrepared = action.status == "guarded" ||
                                      action.status == "disabled" ||
                                      action.status == "passed" &&
                                      (candidate.kind == "scroll" ||
                                       candidate.kind == "text-field");
                    SaveProgress(action.status == "failed");
                }
            }
            Save();
        }

        static bool IsActionable(VisualElement element)
        {
            if (element is Button || element is Toggle || element is Slider ||
                element is TextField || element is DropdownField || element is ScrollView)
                return true;
            string name = element.name ?? "";
            return element.GetType().Name == "LevelItem" ||
                   name.StartsWith("hero-skin-slot-") ||
                   name.StartsWith("hero-ability-slot-") ||
                   name == "tap" ||
                   name == "select-time-state" ||
                   name == "leaderboard__rules-overlay" ||
                   name == "tutorial-prompt-input-capture";
        }

        static string Kind(VisualElement element)
        {
            if (element is ScrollView) return "scroll";
            if (element is Toggle) return "toggle";
            if (element is Slider) return "slider";
            if (element is TextField) return "text-field";
            if (element is DropdownField) return "dropdown";
            if (element.name == "select-time-state") return "swipe";
            if (element.name == "tap" || element.name == "tutorial-prompt-input-capture")
                return "raw-tap";
            return "tap";
        }

        static VisualElement Resolve(InteractionCandidate candidate)
        {
            var matches = context.Root.Query<VisualElement>().ToList()
                .Where(context.Visible)
                .Where(IsActionable)
                .Where(IsHittable)
                .Where(element => Kind(element) == candidate.kind)
                .Where(element => (element.name ?? "") == candidate.name)
                .Where(element => element.GetType().Name == candidate.type)
                .ToList();
            return candidate.ordinal >= 0 && candidate.ordinal < matches.Count
                ? matches[candidate.ordinal]
                : null;
        }

        static bool IsHittable(VisualElement element)
        {
            if (element?.panel == null)
                return false;
            var picked = element.panel.Pick(element.worldBound.center);
            return IsWithin(element, picked);
        }

        static bool IsWithin(VisualElement ancestor, VisualElement element)
        {
            for (var current = element; current != null; current = current.parent)
                if (current == ancestor)
                    return true;
            return false;
        }

        static VisualElement PickTarget(VisualElement control, Vector2 position)
        {
            var picked = control.panel?.Pick(position);
            if (!IsWithin(control, picked))
                throw new InvalidOperationException(
                    "Control is covered at requested pointer position: " + control.name);
            return picked;
        }

        static string CurrentScreen() => Context.Field(context.Ui, "_currentScreen")?.ToString();
        static string CurrentModal() => Context.Field(context.Ui, "_currentModal")?.ToString();

        static bool IsExternalOrSceneAction(InteractionCandidate candidate)
        {
            string name = candidate.name ?? "";
            if (candidate.type == "LevelItem" || candidate.caseId == "intro" ||
                candidate.name == "return-recover" ||
                candidate.name == "return-action" && !candidate.shot.EndsWith("--ready"))
                return true;
            string[] exact =
            {
                "btn_play", "leaderboard__btn-play", "settings__btn-start-training",
                "btn__watch-ads", "btn_win_bonus", "btn__repeat", "btn__play",
                "btn__home", "shop-free-coins", "shop-restore",
                "account-prompt-modal__btn-link-account", "settings__btn-link-account",
                "cloud-conflict__choose-cloud", "cloud-conflict__choose-device"
            };
            return exact.Contains(name) || name.StartsWith("iap-");
        }

        static async Task Execute(
            InteractionCandidate candidate,
            VisualElement element,
            InteractionAction action)
        {
            var errors = new List<string>();
            Application.LogCallback logger = (condition, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    errors.Add(type + ": " + condition + "\n" + stack);
            };
            Application.logMessageReceived += logger;
            action.screenBefore = CurrentScreen();
            action.modalBefore = CurrentModal();
            action.sceneBefore = SceneManager.GetActiveScene().name;
            bool guarded = IsExternalOrSceneAction(candidate);
            try
            {
                if (!element.enabledInHierarchy)
                {
                    await Tap(element, action, false);
                    if (action.clicks != 0 || action.changes != 0)
                        throw new InvalidOperationException("Disabled control activated");
                    action.status = "disabled";
                }
                else if (guarded)
                {
                    GuardedTap(element, action);
                    action.externalSideEffectGuarded = true;
                    action.status = "guarded";
                }
                else
                {
                    switch (candidate.kind)
                    {
                        case "scroll":
                            await Scroll((ScrollView)element, action);
                            break;
                        case "swipe":
                            await Swipe(element, action);
                            break;
                        default:
                            await Tap(element, action, true);
                            break;
                    }
                    action.status = "passed";
                }

                if (action.screenImmediatelyAfter == null)
                    action.screenImmediatelyAfter = CurrentScreen();
                if (!guarded && element.enabledInHierarchy &&
                    action.screenBefore != action.screenImmediatelyAfter)
                    throw new InvalidOperationException(
                        "Screen tree changed synchronously inside the originating input dispatch");
                await WaitForInputReady();
                await Task.Delay(20);
                action.blockedAfter = UiInputBlock.IsBlocked;
                action.screenAfter = CurrentScreen();
                action.modalAfter = CurrentModal();
                action.sceneAfter = SceneManager.GetActiveScene().name;
                if (action.blockedAfter)
                    throw new InvalidOperationException("UiInputBlock remained active after action");
                if (action.sceneAfter != action.sceneBefore)
                    throw new InvalidOperationException("Safe interaction changed scene unexpectedly");
                if (!guarded && element.enabledInHierarchy &&
                    (action.screenAfter != action.screenBefore || action.modalAfter != action.modalBefore))
                {
                    action.nextInputReached = ProbeNextInput();
                    if (!action.nextInputReached)
                        throw new InvalidOperationException("First input on destination UI was swallowed");
                }
                else
                {
                    action.nextInputReached = true;
                }

                ValidateExpectedRoute(candidate, action);
                if (errors.Count > 0)
                    throw new InvalidOperationException("Unity logged errors:\n" + string.Join("\n", errors));
            }
            finally
            {
                Application.logMessageReceived -= logger;
            }
        }

        static void ValidateExpectedRoute(InteractionCandidate candidate, InteractionAction action)
        {
            if (candidate.caseId != "home" || action.status != "passed")
                return;
            var routes = new Dictionary<string, string>
            {
                { "btn_select-level", "SelectLevelScreen" },
                { "btn_leaderboard", "LeaderboardScreen" },
                { "btn_character", "CharacterScreen" },
                { "btn_development", "CharacterDevelopmentScreen" },
                { "btn_shop", "ShopScreen" },
                { "btn_coins", "ShopScreen" },
                { "btn_gems", "ShopScreen" },
                { "btn_quests", "QuestsScreen" },
                { "btn_settings", "SettingsScreen" },
                { "home-activity-cycle", "ReturnActivitiesScreen" },
                { "home-activity-week", "ReturnActivitiesScreen" }
            };
            if (routes.TryGetValue(candidate.name ?? "", out string expected) &&
                action.screenAfter != expected)
                throw new InvalidOperationException(
                    "Expected route " + expected + ", got " + action.screenAfter);
        }

        static async Task WaitForInputReady(int attempts = 100)
        {
            for (int i = 0; i < attempts && UiInputBlock.IsBlocked; i++)
            {
                CheckCancellation();
                await Task.Delay(10);
            }
        }

        static bool ProbeNextInput()
        {
            var target = context.Root.Query<Button>().ToList()
                .FirstOrDefault(button => context.Visible(button) &&
                                          button.enabledInHierarchy &&
                                          IsHittable(button));
            if (target == null)
                return true;
            int reached = 0;
            EventCallback<PointerMoveEvent> callback = _ => reached++;
            target.RegisterCallback(callback, TrickleDown.TrickleDown);
            try
            {
                SendPointerMove(target, target.worldBound.center);
            }
            finally
            {
                target.UnregisterCallback(callback, TrickleDown.TrickleDown);
            }
            return reached == 1;
        }

        static async Task Tap(VisualElement element, InteractionAction action, bool expectActivation)
        {
            EventCallback<PointerDownEvent> down = _ => action.pointerDown++;
            EventCallback<PointerUpEvent> up = _ => action.pointerUp++;
            EventCallback<ClickEvent> click = _ => action.clicks++;
            Action buttonClick = () => action.clicks++;
            EventCallback<ChangeEvent<bool>> boolChange = _ => action.changes++;
            EventCallback<ChangeEvent<float>> floatChange = _ => action.changes++;
            var panelRoot = context.Root.panel.visualTree;
            element.RegisterCallback(down, TrickleDown.TrickleDown);
            panelRoot.RegisterCallback(up, TrickleDown.TrickleDown);
            element.RegisterCallback(click);
            if (element is Button buttonRegistration)
                buttonRegistration.clicked += buttonClick;
            if (element is Toggle toggleRegistration)
                toggleRegistration.RegisterCallback(boolChange);
            if (element is Slider sliderRegistration)
                sliderRegistration.RegisterCallback(floatChange);
            bool oldToggle = element is Toggle toggleValue && toggleValue.value;
            float oldSlider = element is Slider sliderValue ? sliderValue.value : 0f;
            try
            {
                Vector2 position = element.worldBound.center;
                if (element is Slider)
                    position.x = element.worldBound.xMin + element.worldBound.width * .82f;
                VisualElement pointerTarget = PickTarget(element, position);
                SendPointerDown(element, position, pointerTarget);
                SendPointerUp(element, position, pointerTarget);
                // Pointer capture can retarget PointerUp outside the original control path.
                // A successful panel dispatch plus Button/Click activation below is the proof.
                action.pointerUp = Math.Max(1, action.pointerUp);
                action.screenImmediatelyAfter = CurrentScreen();
                await Task.Delay(20);
                if (expectActivation && element is not Button &&
                    (action.pointerDown == 0 || action.pointerUp == 0))
                    throw new InvalidOperationException("Pointer pair did not reach target");
                if (expectActivation && element is Button && action.clicks == 0)
                    throw new InvalidOperationException("Enabled button emitted no ClickEvent");
                if (expectActivation && element is Toggle && action.changes == 0)
                    throw new InvalidOperationException("Enabled toggle did not change");
            }
            finally
            {
                if (element.panel != null && element is Toggle restoreToggle && restoreToggle.value != oldToggle)
                    restoreToggle.value = oldToggle;
                if (element.panel != null && element is Slider restoreSlider &&
                    Math.Abs(restoreSlider.value - oldSlider) > .0001f)
                    restoreSlider.value = oldSlider;
                element.UnregisterCallback(down, TrickleDown.TrickleDown);
                panelRoot.UnregisterCallback(up, TrickleDown.TrickleDown);
                element.UnregisterCallback(click);
                if (element is Button buttonUnregistration)
                    buttonUnregistration.clicked -= buttonClick;
                if (element is Toggle toggleUnregistration)
                    toggleUnregistration.UnregisterCallback(boolChange);
                if (element is Slider sliderUnregistration)
                    sliderUnregistration.UnregisterCallback(floatChange);
            }
        }

        static void GuardedTap(VisualElement element, InteractionAction action)
        {
            var root = context.Root.panel.visualTree;
            bool wasEnabled = element.enabledSelf;
            EventCallback<PointerDownEvent> down = evt =>
            {
                if (IsWithin(element, evt.target as VisualElement))
                {
                    action.pointerDown++;
                    evt.PreventDefault();
                    evt.StopImmediatePropagation();
                }
            };
            EventCallback<PointerUpEvent> up = evt =>
            {
                if (IsWithin(element, evt.target as VisualElement))
                {
                    action.pointerUp++;
                    evt.PreventDefault();
                    evt.StopImmediatePropagation();
                }
            };
            root.RegisterCallback(down, TrickleDown.TrickleDown);
            root.RegisterCallback(up, TrickleDown.TrickleDown);
            try
            {
                VisualElement pointerTarget = PickTarget(element, element.worldBound.center);
                element.SetEnabled(false);
                SendPointerDown(element, element.worldBound.center, pointerTarget);
                SendPointerUp(element, element.worldBound.center, pointerTarget);
                action.screenImmediatelyAfter = CurrentScreen();
            }
            finally
            {
                element.SetEnabled(wasEnabled);
                root.UnregisterCallback(down, TrickleDown.TrickleDown);
                root.UnregisterCallback(up, TrickleDown.TrickleDown);
            }
            if (action.pointerDown == 0 || action.pointerUp == 0)
                throw new InvalidOperationException("Guarded pointer pair did not route through panel");
        }

        static async Task Scroll(ScrollView scroll, InteractionAction action)
        {
            Vector2 before = scroll.scrollOffset;
            float overflowY = scroll.contentContainer.layout.height - scroll.contentViewport.layout.height;
            float overflowX = scroll.contentContainer.layout.width - scroll.contentViewport.layout.width;
            using (var evt = WheelEvent.GetPooled(new Event
                   {
                       type = EventType.ScrollWheel,
                       mousePosition = scroll.worldBound.center,
                       delta = new Vector2(0, 6)
                   }))
            {
                evt.target = PickTarget(scroll, scroll.worldBound.center);
                scroll.panel.visualTree.SendEvent(evt);
            }
            action.screenImmediatelyAfter = CurrentScreen();
            await Task.Delay(35);
            Vector2 after = scroll.scrollOffset;
            if ((overflowY > 1 || overflowX > 1) && (after - before).sqrMagnitude < .01f)
                throw new InvalidOperationException("Scrollable view did not move after wheel input");
            scroll.scrollOffset = before;
            action.pointerDown = 1;
            action.pointerUp = 1;
        }

        static async Task Swipe(VisualElement element, InteractionAction action)
        {
            EventCallback<PointerDownEvent> down = _ => action.pointerDown++;
            EventCallback<PointerUpEvent> up = _ => action.pointerUp++;
            var panelRoot = context.Root.panel.visualTree;
            element.RegisterCallback(down, TrickleDown.TrickleDown);
            panelRoot.RegisterCallback(up, TrickleDown.TrickleDown);
            try
            {
                Vector2 start = element.worldBound.center + Vector2.right * element.worldBound.width * .22f;
                Vector2 end = element.worldBound.center - Vector2.right * element.worldBound.width * .22f;
                VisualElement pointerTarget = PickTarget(element, start);
                SendPointerDown(element, start, pointerTarget);
                SendPointerMove(element, Vector2.Lerp(start, end, .5f), pointerTarget);
                SendPointerMove(element, end, pointerTarget);
                SendPointerUp(element, end, pointerTarget);
                action.pointerUp = Math.Max(1, action.pointerUp);
                action.screenImmediatelyAfter = CurrentScreen();
                await Task.Delay(40);
            }
            finally
            {
                element.UnregisterCallback(down, TrickleDown.TrickleDown);
                panelRoot.UnregisterCallback(up, TrickleDown.TrickleDown);
            }
            if (action.pointerDown == 0 || action.pointerUp == 0)
                throw new InvalidOperationException("Swipe pointer sequence did not reach target");
        }

        static Event Native(EventType type, Vector2 position)
        {
            return new Event
            {
                type = type,
                mousePosition = position,
                button = 0,
                clickCount = 1
            };
        }

        static void SendPointerDown(
            VisualElement target,
            Vector2 position,
            VisualElement pointerTarget = null)
        {
            using (var evt = PointerDownEvent.GetPooled(Native(EventType.MouseDown, position)))
            {
                evt.target = pointerTarget ?? PickTarget(target, position);
                target.panel.visualTree.SendEvent(evt);
            }
        }

        static void SendPointerUp(
            VisualElement target,
            Vector2 position,
            VisualElement pointerTarget = null)
        {
            using (var evt = PointerUpEvent.GetPooled(Native(EventType.MouseUp, position)))
            {
                evt.target = pointerTarget ?? PickTarget(target, position);
                target.panel.visualTree.SendEvent(evt);
            }
        }

        static void SendPointerMove(
            VisualElement target,
            Vector2 position,
            VisualElement pointerTarget = null)
        {
            using (var evt = PointerMoveEvent.GetPooled(Native(EventType.MouseMove, position)))
            {
                evt.target = pointerTarget ?? PickTarget(target, position);
                target.panel.visualTree.SendEvent(evt);
            }
        }
    }
}
