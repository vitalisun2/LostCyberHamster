namespace UiGallery
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Assets.Scripts.System;
    using Assets.Scripts.GameEngine.Mechanics;
    using GameManagement;
    using GameManagement.Progress;
    using GameManagement.Leaderboard;
    using LostCyberHamster.UI;
    using UnityEngine;
    using UnityEngine.UIElements;
    using UnityEditor;
    using Vues.GameCore;

    public static class Fixtures
    {
        public static readonly Dictionary<string, Func<Context, Shot, Task>> Handlers = new Dictionary<string, Func<Context, Shot, Task>>();
        [Serializable] public class Data { public string screen, mode, tab; public int xp = 20, score = 1200, stars = 3, ability = 1, coins; }
        static Data Read(Shot shot) => Newtonsoft.Json.JsonConvert.DeserializeObject<Data>(shot.dataJson);
        public static void Register()
        {
            Handlers["screen"] = async (c, s) => { await c.Screen((ScreenEnum)Enum.Parse(typeof(ScreenEnum), Read(s).screen)); s.method = "Штатный экран, изолированный тестовый профиль"; };
            Handlers["quests"] = Quests;
            Handlers["skills"] = Development;
            Handlers["hero"] = Development;
            Handlers["levelup"] = LevelUp;
            Handlers["win"] = Win;
            Handlers["pause"] = Pause;
            Handlers["lose"] = async (c, s) => { await Game(c); await c.Ui.ShowModalAsync(ScreenEnum.LoseModal); s.method = GameNote + "; текущее состояние сервиса рекламы, реклама не запускалась"; };
        }
        static async Task Quests(Context c, Shot shot)
        {
            var d = Read(shot);
            if (!new[] { "empty", "partial", "ready", "claimed" }.Contains(d.mode)) throw new ArgumentException("Unknown quests mode");
            if (d.tab != "daily" && d.tab != "story") throw new ArgumentException("Unknown quest tab");
            var quests = d.tab == "daily" ? QuestManager.DailyQuests : QuestManager.StoryQuests;
            foreach (var q in quests)
            {
                q.CurrentProgress = d.mode == "empty" ? 0 : d.mode == "partial" ? Math.Max(0, q.TargetAmount - 1) : q.TargetAmount;
                q.IsCompleted = d.mode == "ready" || d.mode == "claimed";
                q.IsRewardClaimed = d.mode == "claimed";
            }
            await c.Screen(ScreenEnum.QuestsScreen);
            // RepaintScreen reloads the default tab. Choose the tab after the reload, not before.
            Context.Call(c.Controller<QuestsScreenController>(), "ShowTab", d.tab == "daily");
            shot.method = "Штатные Quest и контроллер; прогресс и флаги наград подготовлены без Claim";
        }
        static void AbilityProfile(Data d)
        {
            if (!new[] { "tiers", "locked", "no-points", "max", "level-gate" }.Contains(d.mode)) throw new ArgumentException("Unknown ability mode");
            var p = GameDataManager.PlayerData;
            p.PlayerLevel = d.mode == "locked" ? 1 : d.mode == "level-gate" ? 6 : 7;
            p.LastAcknowledgedPlayerLevel = p.PlayerLevel;
            p.DevelopmentPoints = d.mode == "locked" || d.mode == "no-points" ? 0 : 3;
            p.ActiveSuperAttackId = d.mode == "locked" ? 0 : d.ability;
            p.UnlockedSuperAttackIds = d.mode == "locked" ? new List<int>() : new List<int> { 1, 2, 3 };
            p.SuperAttackLevels = Enumerable.Range(1, 3).Select(i => new SuperAttackLevelProgress { SuperAttackId = i, Level = d.mode == "max" ? 3 : d.mode == "tiers" ? i : 1 }).ToList();
        }
        static async Task Development(Context c, Shot s)
        {
            var d = Read(s); AbilityProfile(d);
            await c.Screen(s.fixture == "hero" ? ScreenEnum.CharacterScreen : ScreenEnum.CharacterDevelopmentScreen);
            if (s.fixture == "hero")
            {
                Context.Call(c.Controller<CharacterScreenController>(), "OnAbilityTabClicked", new object[] { null });
                Context.Call(c.Controller<CharacterScreenController>(), "SelectAbility", d.ability);
            }
            s.method = "Штатный экран; уровень, очки, экипировка и ступени способностей подготовлены в тестовом профиле";
        }
        static async Task LevelUp(Context c, Shot s)
        {
            var d = Read(s); var first = d.mode == "first";
            if (!new[] { "first", "normal", "multi" }.Contains(d.mode)) throw new ArgumentException("Unknown levelup mode");
            AbilityProfile(new Data { mode = first ? "locked" : "tiers" });
            var p = GameDataManager.PlayerData; p.PlayerLevel = first ? 2 : 7; p.LastAcknowledgedPlayerLevel = p.PlayerLevel; p.DevelopmentPoints = first ? 1 : 3;
            var modal = c.Controller<LevelUpModalController>();
            modal.SetLevelUpData(first ? 1 : d.mode == "multi" ? 3 : 6, p.PlayerLevel, d.mode == "multi" ? 4 : 1, d.coins);
            modal.SetShieldAction(first ? (Action)(() => { }) : null); modal.SetDevelopmentAction(() => { }); modal.SetOkAction(() => { });
            await c.Ui.ShowModalAsync(ScreenEnum.LevelUpModal);
            s.method = "Штатное окно SetLevelUpData; без начисления XP";
        }
        const string GameNote = "Штатный GameScreen и модалка; статичный игровой фон без мира и персонажа";
        static async Task Game(Context c)
        {
            await c.Screen(ScreenEnum.GameScreen);
            var background = c.Root.Q("background"); var old = background.style.backgroundImage;
            c.Defer(() => background.style.backgroundImage = old);
            background.style.backgroundImage = new StyleBackground(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Content/locations/01_New_York/sprites/backgrounds/bg_new_york_morning.png"));
            var game = c.Controller<GameScreenController>(); game.SetEnergy(60); game.SetHealth(3); game.SetHamsterState(""); game.SetRunResources(35, 2); game.SetRunScore(1200); game.SetUltraValue(40); game.SetUltraControlsVisible(true); game.SetRefillPrices(50, 100); game.SetRefillAvailability(true, true);
        }
        static async Task Win(Context c, Shot s)
        {
            var d = Read(s); await Game(c);
            var flags = BindingFlags.Static | BindingFlags.NonPublic;
            foreach (var name in new[] { "_lastCompletionExperience", "_completionProfile", "_completionGeneration" })
            {
                var field = typeof(LevelManager).GetField(name, flags); var old = field.GetValue(null); c.Defer(() => field.SetValue(null, old));
            }
            typeof(LevelManager).GetField("_lastCompletionExperience", flags).SetValue(null, new ExperienceGrantResult("gallery", d.xp, 7, 7, 0, 0));
            typeof(LevelManager).GetField("_completionProfile", flags).SetValue(null, GameDataManager.ProfileId);
            typeof(LevelManager).GetField("_completionGeneration", flags).SetValue(null, GameDataManager.Generation);
            var modal = c.Controller<WinModalController>(); modal.SetParamsForInit("01_New_York", "Morning", d.stars);
            modal.SetRunResult(new RunResultData(new LevelProgressKey("01_New_York", "Morning", 0), d.score, d.score, true, false, RunResultSubmissionState.Submitted));
            await c.Ui.ShowModalAsync(ScreenEnum.WinModal);
            s.method = GameNote + "; RunResultData и XP заданы без начисления и отправки результата";
        }
        static async Task Pause(Context c, Shot s)
        {
            var d = Read(s);
            if (d.mode != "normal" && d.mode != "tutorial") throw new ArgumentException("Unknown pause mode");
            await Game(c); Context.Call(c.Controller<PauseModalController>(), "SetTutorialMode", d.mode == "tutorial");
            await c.Ui.ShowModalAsync(ScreenEnum.PauseModal); s.method = GameNote + "; SetTutorialMode, игровой цикл не запускался";
        }
    }
}
