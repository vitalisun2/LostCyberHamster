using System;
using System.Collections.Generic;
using Vues.GameCore.Quests;

namespace Vues.GameCore
{
    [Serializable]
    public class QuestData
    {
        public QuestDefinition MvpQuest;
        public List<Quest> DailyTasksPool;
        public List<Quest> StorylineQuests;
    }
}
