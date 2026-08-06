using System;
using Vues.GameCore.Quests;

namespace Vues.GameCore
{
    /// <summary>
    /// Формирует локализованное название квеста из шаблона и его аргументов.
    /// </summary>
    public static class QuestTitleFormatter
    {
        /// <summary>
        /// Локализует шаблон и аргументы названия с fallback на исходные значения.
        /// </summary>
        public static string Format(Quest quest)
        {
            if (quest == null)
            {
                throw new ArgumentNullException(nameof(quest));
            }

            // Локализуем шаблон и каждый контентный аргумент.
            string titleTemplate = LocalizationManager.GetLocalizedString(
                quest.TitleLocalizationKey) ??
                quest.TitleLocalizationKey;
            string[] arguments = quest.TitleLocalizationArguments;
            if (arguments.Length == 0)
            {
                return titleTemplate;
            }

            var localizedArguments = new object[arguments.Length];
            for (int index = 0; index < arguments.Length; index++)
            {
                string argument = arguments[index];
                localizedArguments[index] = string.IsNullOrWhiteSpace(argument)
                    ? string.Empty
                    : LocalizationManager.GetLocalizedString(argument) ??
                      argument;
            }

            // Ошибка контентного шаблона не должна ломать UI.
            try
            {
                return string.Format(titleTemplate, localizedArguments);
            }
            catch (FormatException)
            {
                return titleTemplate;
            }
        }
    }
}
