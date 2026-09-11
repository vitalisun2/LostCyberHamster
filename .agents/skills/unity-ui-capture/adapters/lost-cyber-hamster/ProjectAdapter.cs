// Project-specific preparation. The reusable skill contains no game APIs or screen names.
namespace UiGallery
{
    using System;
    using System.Reflection;
    using System.Threading.Tasks;
    using Assets.Scripts.Entry_Points;
    using GameManagement;
    using LostCyberHamster.UI;
    using UnityEngine;
    using UnityEngine.UIElements;
    using Vues.GameCore;

    public sealed partial class Context
    {
        public UIManager Ui;
        public T Controller<T>() => (T)typeof(UIManager).GetMethod("GetController", BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(typeof(T)).Invoke(Ui, null);
        public void Register(IScreenController controller) => Call(Ui, "AddScreenController", controller);
        public void CloseModal()
        {
            var modal = typeof(UIManager).GetProperty("CurrentModal", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Ui);
            if (modal != null) Ui.CloseModal((ScreenEnum)modal);
        }
        public async Task Screen(ScreenEnum screen)
        {
            await Ui.LoadScreenAsync(ScreenEnum.HomeScreen);
            if (screen != ScreenEnum.HomeScreen) await Ui.LoadScreenAsync(screen);
        }
    }

    public sealed class GalleryProjectAdapter : IProjectAdapter
    {
        bool ownsProfile;
        string original, baseline;
        public bool RestorationVerified { get; private set; } = true;
        public async Task Begin(Context context)
        {
            MenuEntryPoint menu = null;
            object conflictCoordinator = null;
            for (int i = 0; i < 900; i++)
            {
                if (!Application.isPlaying) throw new OperationCanceledException();
                menu = UnityEngine.Object.FindFirstObjectByType<MenuEntryPoint>();
                if (menu != null)
                {
                    conflictCoordinator = Context.Field(menu, "_cloudSaveConflictCoordinator");
                    if (conflictCoordinator != null && GameDataManager.IsLoaded) break;
                }
                await Task.Delay(100);
            }
            if (menu == null || conflictCoordinator == null || !GameDataManager.IsLoaded)
                throw new InvalidOperationException(
                    "Menu initialization timed out after 90s; scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path +
                    "; menu=" + (menu != null) +
                    "; cloudCoordinator=" + (conflictCoordinator != null) +
                    "; gameDataLoaded=" + GameDataManager.IsLoaded);
            if (GameDataManager.HasProgressionTestingBackup) throw new InvalidOperationException("Existing testing backup belongs to another session");
            context.Ui = (UIManager)Context.Field(menu, "_uiManager");
            context.Document = (UIDocument)Context.Field(menu, "_uiDocument");
            menu.enabled = false;
            // OnDisable disposes automatic presenters and UI subscriptions; restore only UI lifecycle.
            context.Ui.SubscribeToEvents(); context.CloseModal();
            typeof(UIManager).GetProperty("HasPriorityPresentation", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(context.Ui, false);
            await context.Screen(ScreenEnum.HomeScreen);
            original = JsonUtility.ToJson(GameDataManager.PlayerData);
            ownsProfile = true; RestorationVerified = false;
            GameDataManager.BeginProgressionTestingProfile(p => {
                p.PlayerLevel = 7; p.LastAcknowledgedPlayerLevel = 7; p.ExperiencePoints = 20;
                p.DevelopmentPoints = 3; p.Money = 500; p.Crystals = 20;
                p.HasUsedTutorialShield = true; p.IsShieldTutorialStarted = false;
                p.HasReceivedTutorialExperience = true;
            });
            await QuestManager.Init();
            baseline = JsonUtility.ToJson(GameDataManager.PlayerData);
            context.Register(new GameScreenController(context.Document));
            context.Register(new WinModalController(context.Document));
            context.Register(new LoseModalController(context.Document));
            context.Register(new PauseModalController(context.Document));
            Fixtures.Register();
            ExtraFixtures.Register();
        }
        public async Task Reset(Context context)
        {
            if (!GameDataManager.IsProgressionTestingProfile) throw new InvalidOperationException("Testing profile lost");
            context.CloseModal();
            JsonUtility.FromJsonOverwrite(baseline, GameDataManager.PlayerData);
            await QuestManager.Init();
            await context.Screen(ScreenEnum.HomeScreen);
        }
        public Task Prepare(Context context, Shot shot)
        {
            if (!Fixtures.Handlers.TryGetValue(shot.fixture, out var prepare)) throw new ArgumentException("Unknown project fixture: " + shot.fixture);
            return prepare(context, shot);
        }
        public void Restore(Context context)
        {
            try { if (context?.Ui != null) context.CloseModal(); }
            finally
            {
                if (ownsProfile)
                {
                    if (GameDataManager.HasProgressionTestingBackup) GameDataManager.RestoreProgressionTestingProfile(endingSession: true);
                    RestorationVerified = !GameDataManager.HasProgressionTestingBackup && original == JsonUtility.ToJson(GameDataManager.PlayerData);
                }
            }
        }
    }
}
