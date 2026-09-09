using Vues.GameCore;
using Vues.GameCore.Quests;

namespace LostCyberHamster.UI
{
    /// <summary>Форматирует локализованный текст карточек без копирования наград и названий в конфигурацию.</summary>
    internal static class NextGoalText
    {
        public static string Get(string key, params object[] args)
        {
            var value = LocalizationManager.GetLocalizedString(key);
            if (string.IsNullOrWhiteSpace(value)) value = key;
            return args.Length == 0 ? value : string.Format(value, args);
        }

        public static string QuestTitle(Quest quest) => QuestTitleFormatter.Format(quest);
    }
}
