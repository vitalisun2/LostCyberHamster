using System;
using System.Collections.Generic;

namespace Vues.GameCore.Quests
{
    [Serializable]
    internal sealed class QuestCatalogData
    {
        public List<QuestDefinition> DailyDefinitions;
        public List<QuestDefinition> StoryDefinitions;
    }
}
