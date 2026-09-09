using System;
using System.Collections.Generic;
using System.Linq;

namespace Vues.GameCore.Quests
{
    /// <summary>Снимок предварительных условий текущей попытки с точной принадлежностью профилю.</summary>
    public sealed class QuestAttemptPreviewSnapshot
    {
        public string ProfileId { get; }
        public long Generation { get; }
        public string AttemptId { get; }
        public bool IsActive => !string.IsNullOrEmpty(AttemptId);
        public IReadOnlyList<QuestAttemptPreview> Quests { get; }

        internal QuestAttemptPreviewSnapshot(string profileId, long generation, string attemptId,
            IEnumerable<QuestAttemptPreview> quests)
        {
            ProfileId = profileId;
            Generation = generation;
            AttemptId = attemptId;
            Quests = Array.AsReadOnly(quests.ToArray());
        }
    }
}
