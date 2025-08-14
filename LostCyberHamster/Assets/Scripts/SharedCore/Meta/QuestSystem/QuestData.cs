using System;
using System.Collections.Generic;

namespace Vues.GameCore
{
    [Serializable]
    public class QuestData
    {
        public List<Quest> DailyTasksPool;
        public List<Quest> StorylineQuests;
    }
}
