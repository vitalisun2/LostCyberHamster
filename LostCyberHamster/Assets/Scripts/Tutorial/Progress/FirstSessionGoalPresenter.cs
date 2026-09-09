using System;
using System.Linq;
using Assets.Scripts.System;
using GameManagement;
using GameManagement.Progress;
using LostCyberHamster.UI;
using Vues.GameCore;
using Vues.GameCore.Quests;

namespace Assets.Scripts.Tutorial
{
    /// <summary>Вычисляет ближайшую цель из сохранённого прогресса и текущего Story; второй прогресс не создаёт.</summary>
    public static class FirstSessionGoalPresenter
    {
        public const string MorningQuestId = QuestExperienceRewardPolicy.FirstMorningQuestId;

        public readonly struct Goal
        {
            public readonly string Id;
            public readonly float? Progress;
            public readonly string Text;
            public readonly string Detail;
            public readonly string ActionText;
            public readonly ScreenEnum Destination;
            public readonly bool BeginsShieldLesson;

            public Goal(string id, string text, string detail, string actionText, ScreenEnum destination,
                bool beginsShieldLesson = false, float? progress = null)
            {
                Id = id;
                Progress = progress;
                Text = text;
                Detail = detail;
                ActionText = actionText;
                Destination = destination;
                BeginsShieldLesson = beginsShieldLesson;
            }
        }

        /// <summary>Возвращает актуальную цель, включая незабранную награду и добровольный Forest-first.</summary>
        public static Goal? GetCurrent()
        {
            var data = GameDataManager.PlayerData;
            if (data == null || !data.IsTutorialCompleted) return null;

            // Онбординг завершается первым дневным уровнем; дальнейшие рекомендации необязательны.
            if (data.Progress.EnumeratePart("01_New_York", "Afternoon")
                .Any(level => level.Key.LevelIndex == 0 && level.IsCompleted)) return null;
            int total = LevelSelectionModel.Create().Locations.FirstOrDefault(location => location.Id == "01_New_York")?
                .Parts.FirstOrDefault(part => part.Id == "Morning")?.TotalLevels ?? 0;
            if (total == 0) return null;
            int morning = data.Progress.EnumeratePart("01_New_York", "Morning").Count(level => level.IsCompleted);
            Quest story = QuestManager.StoryQuests.FirstOrDefault(quest =>
                string.Equals(quest.QuestId, MorningQuestId, StringComparison.Ordinal));
            if (story?.CanClaimReward == true)
                return new Goal("claim", Format("first_session_claim", story.RewardAmount,
                        QuestExperienceRewardPolicy.GetReward(story)), null,
                    Text("first_session_claim_action"), ScreenEnum.QuestsScreen);

            // Первое повышение предлагает щит независимо от количества утренних уровней.
            if (!data.HasUsedTutorialShield && data.PlayerLevel >= 2 &&
                (ShieldTutorialProgress.IsShieldUnlocked ||
                 CharacterDevelopmentService.CanUnlockSuperAttack(ShieldTutorialProgress.ShieldId)))
            {
                if (!ShieldTutorialProgress.IsShieldUnlocked)
                    return new Goal("shield-unlock", Text("first_session_shield_offer"), Text("next_goal_shield_cost"),
                        Text("first_session_skills"), ScreenEnum.CharacterDevelopmentScreen, true);
                if (!ShieldTutorialProgress.IsShieldEquipped)
                    return new Goal("shield-equip", Text("first_session_shield_equip"), null,
                        Text("first_session_hero"), ScreenEnum.CharacterScreen, true);
                return new Goal("shield-ready", Text("first_session_shield_ready"),
                    morning < total ? Format("first_session_morning_continue", morning, total) : Text("first_session_afternoon"),
                    Text("first_session_select_level"), ScreenEnum.SelectLevelScreen);
            }

            // Полное утро и награда следуют фактическому каталогу, а не длительности урока щита.
            if (morning < total)
                return new Goal("morning", Format("first_session_morning", morning, total),
                    Format("first_session_morning_reward", total), Text("first_session_open_quests"), ScreenEnum.QuestsScreen,
                    progress: (float)morning / total);
            if (story == null && data.StoryQuestSet?.ActivePrimaryQuestId == MorningQuestId) return null;
            return new Goal("afternoon", Text("first_session_afternoon"), null,
                Text("first_session_select_level"), ScreenEnum.SelectLevelScreen);
        }

        internal static string Text(string key)
        {
            string value = LocalizationManager.GetLocalizedString(key);
            return string.IsNullOrWhiteSpace(value) ? key : value;
        }

        internal static string Format(string key, params object[] values) => string.Format(Text(key), values);
    }
}
