#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Gameplay;
using GameManagement;
using GameManagement.Leaderboard;
using GameManagement.Progress;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vues.GameCore;
using Vues.GameCore.Quests;

namespace Assets.Scripts.DevTools.ExperienceProgressTesting
{
    /// <summary>Общие команды DEV/Tools: изолированный профиль, production-покупки и снимок runtime.</summary>
    public sealed class AbilityProgressTestingRunner
    {
        public static AbilityProgressTestingRunner Shared { get; } = new();
        public IReadOnlyList<(string Label, Action Execute, Func<bool> Enabled)> Commands { get; }
        private string _result = "Изменяющие команды работают в отдельном профиле. Restore возвращает весь исходный save.";
        private string _snapshot;
        private float _nextSnapshot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void InitializeSession()
        {
            Application.quitting -= Shared.EndSession;
            Application.quitting += Shared.EndSession;
            SuperAttackDropBudget.TestingRoll = null;
        }

        private AbilityProgressTestingRunner()
        {
            var commands = new List<(string, Action, Func<bool>)>
            {
                ("DEV-профиль: L1 / 239 XP", () => Begin(1), () => CanBegin),
                ("DEV-профиль: L6 / 239 XP / tier I", () => Begin(6), () => CanBegin),
                ("DEV-профиль: L7 / 239 XP / tier I", () => Begin(7), () => CanBegin),
                ("Restore исходного профиля", Restore, () => Application.isPlaying && GameDataManager.HasProgressionTestingBackup && InMenu),
                ("Получить +1 XP", GrantOneExperience, () => CanMutate),
                ("Claim первой доступной карточки", ClaimQuest, () => CanMutate),
                ("Claim общей Daily-награды", ClaimDaily, () => CanMutate && QuestManager.CanClaimDailyCommonReward),
                ("Drop roll: 0 (успех до cap)", () => SetRoll(0), () => CanMutate),
                ("Drop roll: 1 (промах)", () => SetRoll(1), () => CanMutate),
                ("Drop roll: обычный random", () => SetRoll(null), () => CanMutate),
                ("W1: показать UTC/retry решения", PreviewWeekly, () => true)
            };
            for (int id = 1; id <= 3; id++)
            {
                int abilityId = id;
                commands.Add(($"Способность {id}: улучшить на один уровень", () => Upgrade(abilityId), () => CanMutate));
                commands.Add(($"Способность {id}: экипировать", () => Select(abilityId), () => CanMutate));
            }
            Commands = commands;
        }

        private static bool InMenu => SceneManager.GetActiveScene().name == "Menu" && !GameDataManager.IsProfileReplacementBlocked;
        private static bool CanBegin => Application.isPlaying && GameDataManager.CanApplyCloudProgress;
        private static bool CanMutate => Application.isPlaying && InMenu && GameDataManager.IsProgressionTestingProfile;

        private void Begin(int level) => Execute(() =>
        {
            GameDataManager.BeginProgressionTestingProfile(player =>
            {
                player.PlayerLevel = level;
                player.ExperiencePoints = 239;
                player.DevelopmentPoints = level == 1 ? 0 : 6;
                player.LastAcknowledgedPlayerLevel = level;
                player.IsTutorialCompleted = true;
                if (level > 1)
                {
                    player.UnlockedSuperAttackIds = new List<int> { 1, 2, 3 };
                    player.SuperAttackLevels = player.UnlockedSuperAttackIds.Select(id =>
                        new SuperAttackLevelProgress { SuperAttackId = id, Level = 1 }).ToList();
                    player.ActiveSuperAttackId = 1;
                }
            });
            SuperAttackDropBudget.TestingRoll = null;
            return "DEV-профиль создан; исходный envelope сохранён. Задания используют обычную ротацию.";
        });

        public void Restore() => Execute(() =>
        {
            SuperAttackDropBudget.TestingRoll = null;
            GameDataManager.RestoreProgressionTestingProfile();
            return "Исходный профиль восстановлен.";
        });

        public void EndSession()
        {
            SuperAttackDropBudget.TestingRoll = null;
            if (!GameDataManager.HasProgressionTestingBackup) return;
            try { GameDataManager.RestoreProgressionTestingProfile(endingSession: true); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private void GrantOneExperience() => ExecuteMutation(() =>
        {
            bool changed = false;
            GameDataManager.ExecuteTransaction(CheckpointReason.CharacterDevelopmentUpgraded,
                () => changed = new PlayerExperienceService().GrantExperienceForTesting(GameDataManager.PlayerData, 1, notify: false),
                () => PlayerExperienceService.PublishCommittedLevelChange(changed, "development_testing"));
            return "+1 XP сохранён production-путём.";
        });

        private void Upgrade(int id) => ExecuteMutation(() =>
        {
            int before = SuperAttackLevelResolver.GetLevel(GameDataManager.PlayerData, id);
            bool upgraded = CharacterDevelopmentService.TryUpgradeSuperAttack(id, before);
            return $"Upgrade {id}: expected={before}, accepted={upgraded}, now={SuperAttackLevelResolver.GetLevel(GameDataManager.PlayerData, id)}";
        });

        private void Select(int id) => ExecuteMutation(() => $"Select {id}: {SuperAttackService.TrySelect(id)}");
        private void SetRoll(float? value) => ExecuteMutation(() =>
        {
            SuperAttackDropBudget.TestingRoll = value;
            return "Roll override: " + (value?.ToString() ?? "random");
        });

        private void ClaimQuest() => ExecuteMutation(() =>
        {
            var quest = QuestManager.DailyQuests.Concat(QuestManager.StoryQuests).FirstOrDefault(item => item.CanClaimReward);
            if (quest == null) return "Готовой карточки нет. Выполните цель через Quest Testing или gameplay.";
            QuestManager.ClaimReward(quest.Id);
            return $"Claim {quest.Id}; XP={QuestExperienceRewardPolicy.GetReward(quest)}";
        });

        private void ClaimDaily() => ExecuteMutation(() =>
        {
            var reward = QuestManager.GetDailyCommonReward();
            return $"Daily set={reward?.SetId}; accepted={QuestManager.ClaimDailyCommonReward(reward)}";
        });

        private void PreviewWeekly() => Execute(() =>
        {
            // Чистая политика production: вычисление решений без отправки, save и наград.
            var journal = new WeeklyLeaderboardJournal();
            var day = DateTime.UtcNow.Date;
            var rows = new List<string>();
            for (int i = 0; i < 4; i++)
            {
                var run = new WeeklyLeaderboardRun { RunId = i == 3 ? "preview-0" : "preview-" + i,
                    OwnerPlayerId = "preview-owner", Environment = "preview", LeaderboardId = "board-" + i };
                var at = i == 0 ? day.AddMinutes(-1) : i == 3 ? day.AddDays(2) : day.AddMinutes(i);
                run.RewardDecision = WeeklyLeaderboardCoordinator.CreateRewardDecision(journal, run, at, legacy: false);
                rows.Add($"{run.RunId} · {at:o}: XP={run.RewardDecision.AwardedExperience}, decision day={run.RewardDecision.UtcDate}");
                if (i != 3) journal.Runs.Add(run);
            }
            return "Предпросмотр политики; сеть/checkpoint не выполнялись.\n" + string.Join("\n", rows);
        });

        private void ExecuteMutation(Func<string> action)
        {
            if (!CanMutate) { _result = "Нужен активный DEV-профиль в Menu."; return; }
            Execute(action);
        }

        private void Execute(Func<string> action)
        {
            _nextSnapshot = 0;
            try { _result = action(); }
            catch (Exception exception) { _result = exception.Message; Debug.LogException(exception); }
        }

        public string Snapshot()
        {
            if (_snapshot != null && Time.realtimeSinceStartup < _nextSnapshot) return _snapshot;
            _nextSnapshot = Time.realtimeSinceStartup + 0.25f;
            return _snapshot = ReadSnapshot();
        }

        private string ReadSnapshot()
        {
            var player = GameDataManager.PlayerData;
            if (!GameDataManager.IsLoaded || player == null) return _result;
            var daily = QuestManager.GetDailyCommonReward();
            var hamster = UnityEngine.Object.FindFirstObjectByType<Hamster>();
            var runtime = hamster != null ? hamster.SuperAttackSnapshot : default;
            var journalJson = GameDataManager.GetJournalJson("weekly");
            var journal = string.IsNullOrEmpty(journalJson) ? new WeeklyLeaderboardJournal() : JsonUtility.FromJson<WeeklyLeaderboardJournal>(journalJson);
            string weekly = string.Join("; ", journal.Runs.Where(run => run.RewardDecision != null).TakeLast(4)
                .Select(run => $"{run.RunId}:{run.RewardDecision.UtcDate}/{run.RewardDecision.AwardedExperience}XP"));
            return $"{_result}\nProfile={GameDataManager.ProfileId}, generation={GameDataManager.Generation}, DEV={GameDataManager.IsProgressionTestingProfile}\n" +
                $"L{player.PlayerLevel}, XP={player.ExperiencePoints}/240, DP={player.DevelopmentPoints}, ack={player.LastAcknowledgedPlayerLevel}\n" +
                $"Route={player.FirstSessionReturnScreen}/{player.FirstSessionReturnLevel}; daily={daily?.SetId}/{daily?.OriginDate}; weekly={weekly}\n" +
                $"Tier: {string.Join(", ", player.SuperAttackLevels.Select(item => item.SuperAttackId + ":" + item.Level))}\n" +
                $"Ability={runtime.AbilityId}/{runtime.Level}, active={runtime.IsActive}, activation={runtime.ActivationId}, time={runtime.Remaining:F2}/{runtime.Duration:F2}, combos={runtime.RemainingCombinations}, landing={runtime.IsFinishing}\n" +
                $"Destroyed={runtime.DestroyedCount}, bonuses={runtime.DropsCreated}, charge={hamster?.UltaChargeAmount.Value}, coins={player.Money}, gems={player.Crystals}, override={SuperAttackDropBudget.TestingRoll?.ToString() ?? "random"}";
        }
    }
}
#endif
