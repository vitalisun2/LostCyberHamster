namespace UiGallery
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading.Tasks;
    using UnityEngine;
    using UnityEngine.UIElements;
    using UnityEditor;
    using Newtonsoft.Json.Linq;
    using LostCyberHamster.UI;
    using GameManagement;
    using GameManagement.Leaderboard;
    using GameManagement.Progress;
    using Unity.Services.Leaderboards.Models;
    using Vues.GameCore;
    using Vues.GameCore.ReturnActivities;
    using Assets.Scripts.Tutorial;
    using Assets.Scripts.GameEngine.Mechanics;

    public static class ExtraFixtures
    {
        static object Static(Type t,string name,params object[] args) => t.GetMethod(name,BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,args);
        static object Get(object o,string n) => Context.Field(o,n);
        static void CallEnum(object o,string n,string value) {var m=o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic);m.Invoke(o,new[]{Enum.Parse(m.GetParameters()[0].ParameterType,value)});}
        static void Show(Context c,string name,bool show) {var e=c.Root.Q(name);if(e==null)throw new Exception("Missing "+name);e.style.display=show?DisplayStyle.Flex:DisplayStyle.None;}
        static async Task Game(Context c) => await (Task)Static(typeof(Fixtures),"Game",c);
        static void CompleteDevelopment()
        {
            var p=GameDataManager.PlayerData;p.PlayerLevel=100;p.UnlockedSkinIds=SkinManager.AvailableSkins.Select(x=>x.Id).ToList();
            p.UnlockedSuperAttackIds=SuperAttackService.Items.Select(x=>x.Id).ToList();
            p.SuperAttackLevels=p.UnlockedSuperAttackIds.Select(x=>new SuperAttackLevelProgress{SuperAttackId=x,Level=3}).ToList();
        }
        public static void Register() => Fixtures.Handlers["extra"]=Prepare;
        public static async Task Prepare(Context c,Shot s)
        {
            var d=JObject.Parse(s.dataJson);string kind=(string)d["kind"], mode=(string)d["mode"]??"normal";
            var p=GameDataManager.PlayerData;
            s.method="Реальные контроллеры и UI; изолированные тестовые данные; визуальные состояния без операций аккаунта, покупок и выдачи наград";
            if(kind=="home")
            {
                p.ExperiencePoints=mode=="empty"?0:mode=="full"?PlayerExperienceService.PlayerLevelThreshold-1:40;
                await c.Screen(ScreenEnum.SettingsScreen);await c.Screen(ScreenEnum.HomeScreen);return;
            }
            if(kind=="intro")
            {
                var go=new GameObject("Capture intro");var intro=go.AddComponent<Intro>();Context.Set(intro,"_uiDocument",c.Document);
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Content/locations/01_New_York/sprites/level_02_intro_1.png");
                Context.Call(intro,"CreateIntroScreen",new List<Sprite>{sprite});
                var root=(VisualElement)Get(intro,"_introScreen");root.name="capture-intro";
                await Task.Delay(300);Context.Call(intro,"UpdateImageLayoutMetrics");Context.Call(intro,"AddImagesToTape");
                foreach(var img in ((List<VisualElement>)Get(intro,"_introImages")))img.style.opacity=1;
                c.Defer(()=>{root.RemoveFromHierarchy();UnityEngine.Object.Destroy(go);});return;
            }
            if(kind=="loading")
            {
                var root=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Content/ui/uxml/Bootstrap.uxml").CloneTree();
                root.name="capture-loading";root.style.position=Position.Absolute;root.style.left=0;root.style.top=0;root.style.right=0;root.style.bottom=0;
                c.Root.Add(root);c.Defer(root.RemoveFromHierarchy);
                int progress=mode=="empty"?0:mode=="full"?100:50;root.Q<ProgressBar>("loading_task__progress").value=progress;
                root.Q<Label>("loading_task__progress-label").text=progress+" %";return;
            }
            if(kind=="development-all")
            {
                CompleteDevelopment();await c.Screen(ScreenEnum.CharacterDevelopmentScreen);
                if(mode=="bottom") {var ctrl=c.Controller<CharacterDevelopmentScreenController>();var scroll=(ScrollView)typeof(CharacterDevelopmentScreenController).GetProperty("SkinScroll",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ctrl);await Task.Delay(200);scroll.scrollOffset=new Vector2(10000,0);}
                return;
            }
            if(kind=="win-extra")
            {
                await Game(c);var modal=c.Controller<WinModalController>();modal.SetParamsForInit("01_New_York","Morning",2);
                modal.SetRunResult(mode=="no-result"?null:new RunResultData(new LevelProgressKey("01_New_York","Morning",4),2500,2500,true,mode=="rankings",mode=="pending"?RunResultSubmissionState.Pending:RunResultSubmissionState.Submitted));
                p.Monetization.LastWinId="capture-win";p.Monetization.LastWinBonusCoins=50;p.Monetization.LastRewardedWinId=mode=="claimed"?"capture-win":null;
                modal.SetRewardedWin(mode=="bonus"||mode=="claimed"?"capture-win":null);
                await c.Ui.ShowModalAsync(ScreenEnum.WinModal);
                if(mode=="pause") {Show(c,"win-ad-pause",true);c.Root.Q("win-modal").AddToClassList("win-has-pause");}
                return;
            }
            if(kind=="board-status")
            {
                await c.Screen(ScreenEnum.LeaderboardScreen);var ctrl=c.Controller<LeaderboardScreenController>();Context.Call(ctrl,"OnUnsubscribeFromEvents");
                var list=Enumerable.Range(0,10).Select(i=>new LeaderboardEntry("c"+i,"Hamster "+i,i,5000-i*200)).ToList();
                Context.Call(ctrl,"RenderResults",list,list[2]);
                var snap=new LeaderboardViewSnapshot{HasTable=true,Top=list,CurrentPlayer=list[2],Status=LeaderboardReadStatus.Ready,NextResetUtc=DateTime.UtcNow.AddDays(4).ToString("O")};
                if(mode=="offline") {snap.Status=(LeaderboardReadStatus)Enum.Parse(typeof(LeaderboardReadStatus),"Offline");snap.IsStale=true;}
                if(mode=="refreshing")snap.IsRefreshing=true;
                if(mode=="profile")snap.ParticipationStatus=LeaderboardParticipationStatus.ProfileRequired;
                if(mode=="sync")snap.ParticipationStatus=LeaderboardParticipationStatus.Synchronizing;
                if(mode=="no-entry")snap.PersonalStatus=LeaderboardPersonalStatus.NoEntry;
                Context.Call(ctrl,"RenderStatus",snap);return;
            }
            if(kind=="level-cards")
            {
                await c.Screen(ScreenEnum.SelectLevelScreen);var ctrl=c.Controller<SelectLevelScreenController>();var loc=Get(ctrl,"_selectedLocationView");
                var parts=((System.Collections.IEnumerable)loc.GetType().GetProperty("Parts").GetValue(loc)).Cast<object>().ToList();
                if(mode=="parts-open") {int i=0;foreach(var card in c.Root.Query<LevelItem>().ToList()){string part=new[]{"Morning","Afternoon","Evening","Night"}[i++%4];card.ConfigureForPart(part,part,true);}return;}
                var selected=parts.First();using(var ev=ClickEvent.GetPooled())Context.Call(ctrl,"OnDayPartClicked",ev,loc,selected);
                var cards=c.Root.Query<LevelItem>().ToList();int n=0;
                foreach(var card in cards)
                {
                    var model=(LevelProgress)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(LevelProgress));
                    Context.Set(model,"<IsUnlocked>k__BackingField",true);Context.Set(model,"<Stars>k__BackingField",n%4);Context.Set(model,"<LevelKey>k__BackingField","level-"+n);
                    card.ConfigureForLevel(model,++n);
                }
                return;
            }
            if(kind=="skin")
            {
                var ids=SkinManager.AvailableSkins.Select(x=>x.Id).ToList();p.PlayerLevel=20;
                p.UnlockedSkinIds=mode=="locked"?new List<int>{0}:ids;
                p.PurchasedSkinIds=mode=="owned"?ids:new List<int>{0};p.Crystals=mode=="poor"?0:10000;
                await c.Screen(ScreenEnum.CharacterScreen);
                if(mode=="bottom") {await Task.Delay(200);var scroll=c.Root.Q<ScrollView>("hero-skin-scroll");if(scroll==null)scroll=c.Root.Query<ScrollView>().ToList().First(x=>c.Visible(x));scroll.scrollOffset=new Vector2(0,10000);}
                return;
            }
            if(kind=="ability")
            {
                Static(typeof(Fixtures),"AbilityProfile",new Fixtures.Data{mode="tiers",ability=1});
                p.ActiveSuperAttackId=mode=="unequipped"?0:1;
                await c.Screen(ScreenEnum.CharacterScreen);
                Context.Call(c.Controller<CharacterScreenController>(),"OnAbilityTabClicked",new object[]{null});
                Context.Call(c.Controller<CharacterScreenController>(),"SelectAbility",mode=="other"?2:1);return;
            }
            if(kind=="upgrade")
            {
                Static(typeof(Fixtures),"AbilityProfile",new Fixtures.Data{mode=mode,ability=1});
                await c.Screen(ScreenEnum.CharacterDevelopmentScreen);var modal=c.Controller<AbilityUpgradeModalController>();
                modal.SetAbility(1);await c.Ui.ShowModalAsync(ScreenEnum.AbilityUpgradeModal);return;
            }
            if(kind=="settings")
            {
                await c.Screen(ScreenEnum.SettingsScreen);var ctrl=c.Controller<SettingsScreenController>();
                if(mode=="linked"||mode=="guest"||mode=="signing") CallEnum(ctrl,"UpdateAccountState",mode=="linked"?"Linked":mode=="guest"?"Guest":"SigningIn");
                if(mode=="sync") Context.Call(ctrl,"UpdateCloudSyncStatus",Enum.Parse(typeof(SettingsScreenController).GetMethod("UpdateCloudSyncStatus",BindingFlags.Instance|BindingFlags.NonPublic).GetParameters()[0].ParameterType,"Synchronizing"));
                if(mode=="edit"||mode=="error"||mode=="busy")
                {
                    Context.Call(ctrl,"SetPlayerNameEditMode",true);
                    c.Root.Q<TextField>("settings__txt-player-name").SetValueWithoutNotify("CyberHamster");
                    if(mode=="error")Context.Call(ctrl,"ShowPlayerNameError","player_name_validation_error");
                    if(mode=="busy")Context.Call(ctrl,"SetPlayerNameBusy",true);
                }
                if(mode=="off")foreach(var name in new[]{"music","sound","notifications"})
                {c.Root.Q<Toggle>("settings__cbx-"+name).SetValueWithoutNotify(false);c.Root.Q("settings__"+name+"-checkbox").EnableInClassList("settings-checkbox--checked",false);}
                if(mode=="language") {var field=c.Root.Q<DropdownField>("settings__dd-languages");var m=typeof(BasePopupField<string,string>).GetMethod("ShowMenu",BindingFlags.Instance|BindingFlags.NonPublic);m.Invoke(field,null);}
                if(mode=="bottom") {await Task.Delay(200);c.Root.Q<ScrollView>("settings__scroll-view").scrollOffset=new Vector2(0,10000);}
                return;
            }
            if(kind=="shop")
            {
                p.Money=mode=="poor"?0:10000;p.Monetization.StarterPackOwned=mode=="owned";p.Monetization.NoAds=mode=="owned";
                p.Monetization.LastShopRewardUtcTicks=mode=="cooldown"?DateTime.UtcNow.Ticks:0;
                await c.Screen(ScreenEnum.ShopScreen);var ctrl=c.Controller<ShopScreenController>();
                if(mode=="ready") {Action enable=()=>{foreach(var name in new[]{"iap-noads","iap-crystals10","iap-crystals30","iap-starter","shop-free-coins","shop-crystal-pack","shop-restore"})c.Root.Q<Button>(name)?.SetEnabled(true);};enable();var timer=c.Root.schedule.Execute(enable).Every(50);c.Defer(timer.Pause);}
                if(mode=="message")Context.Call(ctrl,"ShowPurchaseMessage","iap_purchase_failed");
                if(mode=="bottom"||mode=="message") {await Task.Delay(200);c.Root.Q<ScrollView>("shop-offers").scrollOffset=new Vector2(0,10000);}
                return;
            }
            if(kind=="levels")
            {
                await c.Screen(ScreenEnum.SelectLevelScreen);var ctrl=c.Controller<SelectLevelScreenController>();
                if(mode=="next")Context.Call(ctrl,"ChangeLocation",1);
                if(mode=="levels")
                {
                    var loc=Get(ctrl,"_selectedLocationView");var parts=(System.Collections.IEnumerable)loc.GetType().GetProperty("Parts").GetValue(loc);
                    var part=parts.Cast<object>().First();
                    using(var ev=ClickEvent.GetPooled())Context.Call(ctrl,"OnDayPartClicked",ev,loc,part);
                }
                return;
            }
            if(kind=="board")
            {
                await c.Screen(ScreenEnum.LeaderboardScreen);var ctrl=c.Controller<LeaderboardScreenController>();
                Context.Call(ctrl,"OnUnsubscribeFromEvents");
                if(mode=="loading")Context.Call(ctrl,"ShowLoading");
                else if(mode=="error")Context.Call(ctrl,"ShowError","leaderboard_error");
                else if(mode=="empty")Context.Call(ctrl,"ShowEmpty");
                else if(mode=="unavailable")Context.Call(ctrl,"ShowUnavailable");
                else
                {
                    var list=Enumerable.Range(0,20).Select(i=>new LeaderboardEntry("capture-"+i,"Hamster "+(i+1),i,12000-i*200)).ToList();
                    var player=mode=="outside"?new LeaderboardEntry("capture-player","CyberHamster",73,1600):list[2];
                    Context.Call(ctrl,"RenderResults",list,player);
                    if(mode=="rules"){Context.Set(ctrl,"_viewActive",true);using(var ev=ClickEvent.GetPooled())Context.Call(ctrl,"OnClickRules",ev);}
                    if(mode=="bottom"){await Task.Delay(200);c.Root.Q<ScrollView>("leaderboard__rows").scrollOffset=new Vector2(0,10000);}
                }
                return;
            }
            if(kind=="cloud")
            {
                var modal=c.Controller<CloudSaveConflictModalController>();
                var local=new CloudSaveConflictCardDto(4,500,20,DateTime.Now,7);
                modal.SetData(new CloudSaveConflictModalDto(mode=="no-cloud"?null:new CloudSaveConflictCardDto(12,1800,50,DateTime.Now,12),local));
                modal.SetBusy(false);modal.SetError(null);await c.Ui.ShowModalAsync(ScreenEnum.CloudSaveConflictModal);
                if(mode=="busy")modal.SetBusy(true);
                if(mode=="error")modal.SetError("cloud_sync_status_unavailable");return;
            }
            if(kind=="account") {await c.Ui.ShowModalAsync(ScreenEnum.AccountPromptModal);return;}
            if(kind=="ownership")
            {
                var prompt=mode=="signin"?ProfileOwnershipPrompt.ShowExistingAccountConfirmation(c.Root,()=>{}):ProfileOwnershipPrompt.Show(c.Root,()=>{});
                c.Defer(prompt.Dispose);
                if(mode=="conflict")c.Root.Q<Button>("profile-ownership__use-local").SetEnabled(false);return;
            }
            if(kind=="pause-error") {await Game(c);await c.Ui.ShowModalAsync(ScreenEnum.PauseModal);Context.Call(c.Controller<PauseModalController>(),"ShowExitError",mode=="resume");return;}
            if(kind=="lose")
            {
                await Game(c);var modal=c.Controller<LoseModalController>();modal.SetReviveActions(()=>mode!="exhausted",()=>mode=="ready",()=>{});
                await c.Ui.ShowModalAsync(ScreenEnum.LoseModal);
                if(mode=="ready")c.Root.Q<Button>("btn__watch-ads").SetEnabled(true);return;
            }
            if(kind=="journey") {if(mode=="complete")CompleteDevelopment();try{c.Controller<JourneyCompleteModalController>();}catch{c.Register(new JourneyCompleteModalController(c.Document));}await c.Ui.ShowModalAsync(ScreenEnum.JourneyCompleteModal);return;}
            if(kind=="levelup-complete")
            {
                CompleteDevelopment();var modal=c.Controller<LevelUpModalController>();modal.SetLevelUpData(99,100,0,100);modal.SetShieldAction(null);await c.Ui.ShowModalAsync(ScreenEnum.LevelUpModal);return;
            }
            if(kind=="daily-reward")
            {
                foreach(var q in QuestManager.DailyQuests){q.CurrentProgress=q.TargetAmount;q.IsCompleted=true;q.IsRewardClaimed=true;}
                await c.Ui.ShowModalAsync(ScreenEnum.DailyQuestRewardModal);
                c.Root.Q<Button>("btn_daily_quest_reward_claim").SetEnabled(mode=="ready");return;
            }
            if(kind=="activity-reward")
            {
                var reward=new ActivityReward{Id="capture",Kind="cycle",OriginDay=ActivityDayPolicy.Day(ReturnActivityService.UtcNow),Cycle=1,Step=1,Coins=50,Gems=2};
                c.Controller<ActivityRewardModalController>().SetReward(new ActivityRewardSnapshot(reward));
                await c.Ui.ShowModalAsync(ScreenEnum.ActivityRewardModal);
                c.Root.Q<Button>("return-reward-claim").SetEnabled(mode!="busy");return;
            }
            if(kind=="return")
            {
                string tab=(string)d["tab"]??"cycle";
                var now=ReturnActivityService.UtcNow;var day=ActivityDayPolicy.Day(now);var state=p.ReturnActivities;
                state.Normalize();state.Revision++;state.MaxObservedDay=day;state.LastCreditedDay=day;
                state.CycleRewards=ReturnActivityConfig.Current.Days.ToList();state.TotalDays=mode=="empty"?0:mode=="complete"?7:3;
                state.Week=new ActivityWeekState{Id=ActivityDayPolicy.Week(now),TargetWins=5,TargetDays=3,Coins=200};
                state.Week.AttemptIds=Enumerable.Range(0,mode=="empty"?0:mode=="complete"?5:2).Select(i=>"capture"+i).ToList();
                state.Week.Days=Enumerable.Range(0,mode=="empty"?0:mode=="complete"?3:1).Select(i=>ActivityDayPolicy.Day(now.AddDays(-i))).ToList();
                state.Week.Completed=mode=="complete";state.Rewards.Clear();
                for(int i=1;i<=state.TotalDays;i++)state.Rewards.Add(new ActivityReward{Id="capture-"+i,Kind="cycle",Cycle=1,Step=i,Coins=50,Gems=i==7?2:0,OriginDay=day,Claimed=mode!="ready",Presented=mode!="ready"});
                ReturnActivitiesScreenController.InitialKind=tab;await c.Screen(ScreenEnum.ReturnActivitiesScreen);
                ((IVisualElementScheduledItem)Get(c.Controller<ReturnActivitiesScreenController>(),"_timer"))?.Pause();
                if(mode=="ready")c.Root.Q<Button>("return-action").SetEnabled(true);
                if(mode=="recovery"){c.Root.Q("return-screen").AddToClassList("return-screen--recovery");Show(c,"return-recover",true);c.Root.Q<Label>("return-status").text="Восстановите историю активности";}
                return;
            }
            if(kind=="hud")
            {
                await Game(c);var game=c.Controller<GameScreenController>();
                if(mode=="empty"){game.SetHealth(0);game.SetEnergy(0);game.SetUltraValue(0);game.SetRefillAvailability(false,false);}
                if(mode=="full"){game.SetHealth(3);game.SetEnergy(100);game.SetUltraValue(100);}
                if(mode=="hidden")game.SetUltraControlsVisible(false);
                if(mode=="active")game.SetAbilityActivity(new SuperAttackRuntimeSnapshot(1,2,1,true,5,10));
                if(mode=="combos"||mode=="finishing")game.SetAbilityActivity(new SuperAttackRuntimeSnapshot(3,2,1,true,3,10,2,mode=="finishing"));
                return;
            }
            if(kind=="tutorial")
            {
                await Game(c);var view=new TutorialGameplayView(c.Root);c.Defer(view.Dispose);
                if(mode=="complete"||mode=="error")view.ShowCompletion(mode=="error"?"Не удалось сохранить":"Обучение завершено","Продолжай приключение","Продолжить",mode=="error");
                else {int step=mode=="jump"?2:1;view.ShowHeader("tutorial_step_"+step+"_title",step);if(mode!="header")view.ShowPrompt("tutorial_step_"+step+"_instruction",mode=="tap"?TutorialAction.Tap:TutorialAction.Jump);}
                return;
            }
            if(kind=="coach")
            {
                var t=typeof(TutorialGameplayView).Assembly.GetType("Assets.Scripts.Tutorial.FirstSessionCoachView");
                var coach=Activator.CreateInstance(t,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{c.Root},null);
                c.Defer(()=>((IDisposable)coach).Dispose());
                Context.Call(coach,"Show","Первые шаги","Выполни задание и забери награду","Открыть задания",(Action)(()=>{}),mode=="two"?"Позже":null,null,mode=="goal",null);return;
            }
            if(kind=="notification")
            {
                bool gameplay=mode=="game";if(gameplay)await Game(c);
                var host=new FirstSessionNotificationHost(c.Ui,c.Root);c.Defer(host.Dispose);
                var queue=(NotificationCoordinator)Get(host,"_notifications");
                queue.Enqueue(new NotificationMessage("capture", "Задание выполнено", mode=="compact"?null:"Награда доступна в заданиях", durationSeconds:30,allowedDuringGameplay:true));
                await Task.Delay(500);host.Tick(gameplay,false);return;
            }
            throw new Exception("Unknown extra fixture "+kind);
        }
    }
}
