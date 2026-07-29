namespace Vues.GameCore.Quests
{
    /// <summary>
    /// Неизменяемые данные квеста для отображения.
    /// </summary>
    public sealed class QuestViewData
    {
        public string Id { get; }
        public string Title { get; }
        public int CurrentProgress { get; }
        public int TargetAmount { get; }
        public ResourceType RewardType { get; }
        public int RewardAmount { get; }
        public bool IsCompleted { get; }
        public bool IsRewardClaimed { get; }

        public QuestViewData(
            QuestDefinition definition,
            QuestState state)
        {
            Id = definition.Id;
            Title = definition.Title;
            CurrentProgress = state.CurrentProgress;
            TargetAmount = definition.TargetAmount;
            RewardType = definition.RewardType;
            RewardAmount = definition.RewardAmount;
            IsCompleted = state.IsCompleted;
            IsRewardClaimed = state.IsRewardClaimed;
        }
    }
}
