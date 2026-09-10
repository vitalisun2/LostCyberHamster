namespace UiGallery
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using UnityEditor;
    using Newtonsoft.Json;
    using UnityEngine;
    using UnityEngine.UIElements;

    [Serializable] public class Shot
    {
        public string id, fixture, dataJson, file, method;
        public string[] expected, expectedText;
        public int settleMs = 600;
    }
    [Serializable] public class Plan { public Shot[] shots; }
    [Serializable] public class Evidence
    {
        public string name;
        public float x, y, width, height;
    }
    [Serializable] public class Frame
    {
        public string id, file, method, error, visibleText;
        public int width, height;
        public Evidence[] elements;
        public bool success;
    }
    [Serializable] public class Result
    {
        public string phase = "starting", current, error, cleanupError;
        public bool done, restored;
        public List<Frame> frames = new List<Frame>();
    }

    public sealed partial class Context
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        public UIDocument Document;
        public VisualElement Root => Document.rootVisualElement;
        public Func<Shot, Task<Evidence[]>> Probe;
        public Func<string> TextProbe;
        public string ReadVisibleText() => TextProbe != null ? TextProbe() : string.Join("\n", Root.Query<TextElement>().ToList().Where(Visible).Select(e => e.text));
        readonly Stack<Action> cleanup = new Stack<Action>();
        public static object Field(object obj, string name) => obj.GetType().GetField(name, Flags).GetValue(obj);
        public static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Flags).SetValue(obj, value);
        public static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Flags).Invoke(obj, args);
        public void Defer(Action action) => cleanup.Push(action);
        public void ResetViews()
        {
            var failures = new List<Exception>();
            while (cleanup.Count > 0) { try { cleanup.Pop()(); } catch (Exception e) { failures.Add(e); } }
            if (failures.Count > 0) throw new AggregateException(failures);
        }
        public bool Visible(VisualElement element)
        {
            if (element == null || element.panel == null || element.worldBound.width < 1 || element.worldBound.height < 1) return false;
            if (!element.worldBound.Overlaps(Root.worldBound)) return false;
            for (var node = element; node != null; node = node.parent)
                if (node.resolvedStyle.display == DisplayStyle.None || node.resolvedStyle.visibility == Visibility.Hidden || node.resolvedStyle.opacity <= 0) return false;
            return true;
        }
        public async Task<Evidence[]> Settle(Shot shot)
        {
            await Task.Delay(shot.settleMs);
            if (Probe != null) return await Probe(shot);
            string previous = null;
            int stable = 0;
            for (int attempt = 0; attempt < 30; attempt++)
            {
                var elements = shot.expected.Select(name => Root.Q(name)).ToArray();
                if (elements.All(Visible))
                {
                    var bounds = elements.Select(e => new Evidence { name = e.name, x = e.worldBound.x, y = e.worldBound.y, width = e.worldBound.width, height = e.worldBound.height }).ToArray();
                    var key = string.Join("|", bounds.Select(b => $"{b.x:F1},{b.y:F1},{b.width:F1},{b.height:F1}"));
                    stable = key == previous ? stable + 1 : 0;
                    previous = key;
                    if (stable >= 2) return bounds;
                }
                await Task.Delay(100);
            }
            throw new InvalidOperationException("UI missing or unstable: " + string.Join(", ", shot.expected));
        }
    }

    public interface IProjectAdapter
    {
        Task Begin(Context context);
        Task Reset(Context context);
        Task Prepare(Context context, Shot shot);
        void Restore(Context context);
        bool RestorationVerified { get; }
    }

    public static class Capture
    {
        const string PlanPath = __PLAN_PATH__;
        const string OutputPath = __OUTPUT_PATH__;
        const string TaskKey = __TASK_KEY__;
        static Result result;
        static Context context;
        static IProjectAdapter adapter;
        static bool cleaned;
        static void Save() => File.WriteAllText(Path.Combine(OutputPath, "capture-result.json"), JsonConvert.SerializeObject(result, Formatting.Indented));
        static void CheckCancellation()
        {
            if (!Application.isPlaying || object.Equals(AppDomain.CurrentDomain.GetData(TaskKey + ":cancel"), true))
                throw new OperationCanceledException("Capture cancelled");
        }
        public static string Start()
        {
            AppDomain.CurrentDomain.SetData(TaskKey, Run());
            return "started";
        }
        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode) Cleanup();
        }
        static void Cleanup()
        {
            if (cleaned) return;
            cleaned = true;
            result.phase = "restoring"; Save();
            try { context?.ResetViews(); } catch (Exception e) { result.cleanupError = e.ToString(); }
            try
            {
                adapter?.Restore(context);
                result.restored = adapter == null || adapter.RestorationVerified;
                if (!result.restored) throw new InvalidOperationException("Adapter restoration verification failed");
            }
            catch (Exception e) { result.cleanupError = (result.cleanupError ?? "") + "\n" + e; }
            EditorApplication.playModeStateChanged -= OnPlayMode;
            result.done = true;
            result.phase = result.error == null && result.cleanupError == null ? "complete" : "failed";
            Save();
        }
        static async Task Run()
        {
            result = new Result(); Save();
            try
            {
                CheckCancellation();
                var plan = JsonConvert.DeserializeObject<Plan>(File.ReadAllText(PlanPath));
                if (plan?.shots == null || plan.shots.Length == 0) throw new ArgumentException("Capture plan is empty");
                context = new Context();
                adapter = new GalleryProjectAdapter();
                EditorApplication.playModeStateChanged += OnPlayMode;
                await adapter.Begin(context);
                CheckCancellation();
                result.phase = "capturing"; Save();
                foreach (var shot in plan.shots)
                {
                    result.current = shot.id; Save();
                    var frame = new Frame { id = shot.id, file = shot.file };
                    try
                    {
                        CheckCancellation();
                        context.ResetViews();
                        await adapter.Reset(context);
                        CheckCancellation();
                        await adapter.Prepare(context, shot);
                        CheckCancellation();
                        frame.elements = await context.Settle(shot);
                        CheckCancellation();
                        frame.visibleText = context.ReadVisibleText();
                        foreach (var text in shot.expectedText ?? Array.Empty<string>())
                            if (!frame.visibleText.Contains(text)) throw new InvalidOperationException("Expected visible text missing: " + text);
                        frame.width = Screen.width; frame.height = Screen.height; frame.method = shot.method;
                        string path = Path.Combine(OutputPath, shot.file);
                        ScreenCapture.CaptureScreenshot(path);
                        for (int i = 0; i < 100 && !File.Exists(path); i++) { CheckCancellation(); await Task.Delay(100); }
                        if (!File.Exists(path)) throw new IOException("Screenshot not written");
                        await Task.Delay(150);
                        frame.success = true;
                    }
                    catch (Exception e) { frame.error = e.ToString(); }
                    result.frames.Add(frame); Save();
                    if (!frame.success) throw new InvalidOperationException("Capture failed: " + shot.id);
                }
            }
            catch (Exception e) { result.error = e.ToString(); }
            finally { Cleanup(); }
        }
    }
}
