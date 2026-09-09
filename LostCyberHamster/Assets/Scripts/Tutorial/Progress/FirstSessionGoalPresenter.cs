using System;
using System.Linq;
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
            public readonly string Text;
            public readonly string Detail;
            public readonly string ActionText;
            public readonly ScreenEnum Destination;
            public readonly bool BeginsShieldLesson;

            public Goal(string text, string detail, string actionText, ScreenEnum destination,
                bool beginsShieldLesson = false)
            {
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

            // Считаем разные пройденные уровни по общему snapshot, а Claim — по владельцу квестов.
            int morning = data.Progress.EnumeratePart("01_New_York", "Morning").Count(level => level.IsCompleted);
            Quest story = QuestManager.StoryQuests.FirstOrDefault(quest =>
                string.Equals(quest.QuestId, MorningQuestId, StringComparison.Ordinal));
            if (morning < 3)
                return new Goal(Format("first_session_morning", morning),
                    Text("first_session_morning_reward"), Text("first_session_open_quests"), ScreenEnum.QuestsScreen);
            if (story == null && data.StoryQuestSet?.ActivePrimaryQuestId == MorningQuestId) return null;
            if (story?.CanClaimReward == true)
                return new Goal(Format("first_session_claim", story.RewardAmount,
                        QuestExperienceRewardPolicy.GetReward(story)), null,
                    Text("first_session_claim_action"), ScreenEnum.QuestsScreen);

            // Открытие, экипировка и фактическое применение остаются разными состояниями.
            if (!data.HasUsedTutorialShield)
            {
                if (!ShieldTutorialProgress.IsShieldUnlocked)
                    return data.DevelopmentPoints > 0
                        ? new Goal(Text("first_session_shield_offer"), Text("first_session_shield_unlock"),
                            Text("first_session_skills"), ScreenEnum.CharacterDevelopmentScreen, true)
                        : new Goal(Text("first_session_shield_wait"), null,
                            Text("first_session_open_quests"), ScreenEnum.QuestsScreen);
                if (!ShieldTutorialProgress.IsShieldEquipped)
                    return new Goal(Text("first_session_shield_equip"), null,
                        Text("first_session_hero"), ScreenEnum.CharacterScreen, true);
                return new Goal(Text("first_session_shield_ready"), Text("first_session_afternoon"),
                    Text("first_session_select_level"), ScreenEnum.SelectLevelScreen);
            }

            bool firstAfternoonCompleted = data.Progress.EnumeratePart("01_New_York", "Afternoon")
                .Any(level => level.Key.LevelIndex == 0 && level.IsCompleted);
            return firstAfternoonCompleted ? null : new Goal(Text("first_session_afternoon"), null,
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
