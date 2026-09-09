#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using GameManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vues.GameCore.ReturnActivities;

namespace Assets.Scripts.DevTools.ReturnActivityTesting
{
    /// <summary>Общие ручные DEV/Tools-команды; изменения разрешены только в изолированном профиле.</summary>
    public sealed class ReturnActivityTestingRunner
    {
        public static ReturnActivityTestingRunner Shared { get; } = new();
        public string Status { get; private set; } = "Откройте Menu. Начните изолированную сессию; завершение вернёт исходное сохранение.";
        public bool CanBegin => Application.isPlaying && SceneManager.GetActiveScene().name == "Menu" &&
            GameDataManager.CanApplyCloudProgress && !GameDataManager.HasProgressionTestingBackup;
        public bool CanChange => Application.isPlaying && SceneManager.GetActiveScene().name == "Menu" &&
            GameDataManager.IsProgressionTestingProfile;
        public event Action Changed;

        public void Begin() => Run(() =>
        {
            if (!CanBegin) throw new InvalidOperationException("Нужен Menu и завершённое сохранение.");
            GameDataManager.BeginProgressionTestingProfile(data =>
            {
                data.IsFirstLaunch = false; data.IsTutorialCompleted = true; data.IsTutorialSkipped = true;
                data.HasReceivedTutorialExperience = true; data.ReturnActivities = new ReturnActivityState();
            });
            ReturnActivityService.DevelopmentUtc = ActivityDayPolicy.WeekStart(DateTime.UtcNow).AddHours(12);
            ReturnActivityService.RefreshPeriods();
        });

        public void Restore() => Run(() =>
        {
            GameDataManager.RestoreProgressionTestingProfile();
            ReturnActivityService.DevelopmentUtc = null;
        });

        public void Win() => Run(() =>
        {
            RequireSession();
            var attempt = new ActivityAttemptContext(GameDataManager.PlayerData.CurrentLevel);
            if (!attempt.Prepare()) throw new InvalidOperationException("Попытка недоступна.");
            GameDataManager.ExecuteTransaction(CheckpointReason.ReturnActivityProgressed,
                () => ReturnActivityService.ApplyCommittedWin(attempt, attempt.Level, 1),
                ReturnActivityService.PublishChanged);
        });

        public void NextDay() => MoveDays(1);
        public void PreviousDay() => MoveDays(-1);
        public void NextWeek() => MoveDays(7);
        private void MoveDays(int days) => Run(() =>
        {
            RequireSession();
            ReturnActivityService.DevelopmentUtc = ReturnActivityService.UtcNow.AddDays(days);
            ReturnActivityService.RefreshPeriods();
        });

        public void Claim() => Run(() =>
        {
            RequireSession();
            var reward = ReturnActivityRewardService.GetRewards().FirstOrDefault();
            if (reward == null) throw new InvalidOperationException("Нет заработанной награды.");
            var result = ReturnActivityRewardService.Claim(reward);
            if (result != ActivityClaimResult.Claimed && result != ActivityClaimResult.AlreadyClaimed)
                throw new InvalidOperationException(result.ToString());
            ReturnActivityRewardService.Acknowledge(reward);
        });

        public void Inspect()
        {
            var state = ReturnActivityService.GetSnapshot();
            Status = state == null ? "Профиль не загружен." :
                $"UTC {ReturnActivityService.UtcNow:O}\nИзолированный: {GameDataManager.IsProgressionTestingProfile}\n" +
                $"Дни {state.TotalDays}; цикл {state.Cycle}, шаг {state.Step}/7; last {state.LastCreditedDay}\n" +
                $"Неделя {state.Week.Id}: {state.Week.AttemptIds.Count}/{state.Week.TargetWins} побед, " +
                $"{state.Week.Days.Count}/{state.Week.TargetDays} дней; completed={state.Week.Completed}\n" +
                $"Готово {state.Rewards.Count(reward => !reward.Claimed)}; " +
                $"монеты {GameDataManager.PlayerData.Money}; кристаллы {GameDataManager.PlayerData.Crystals}; " +
                $"XP {GameDataManager.PlayerData.ExperiencePoints}; recovery={ReturnActivityRecovery.IsRequired}";
            Changed?.Invoke();
        }

        private static void RequireSession()
        {
            if (!Shared.CanChange) throw new InvalidOperationException("Нужен изолированный профиль в Menu.");
        }
        private void Run(Action action)
        {
            try { action(); Inspect(); }
            catch (Exception exception) { Status = exception.Message; Changed?.Invoke(); }
        }
    }
}
#endif
