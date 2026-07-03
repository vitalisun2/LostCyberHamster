namespace Assets.Scripts.Tutorial
{
    public enum TutorialUiFlowResultStatus
    {
        Ignored,
        Stayed,
        Advanced,
        Completed
    }

    public sealed class TutorialUiFlowResult
    {
        private TutorialUiFlowResult(TutorialUiFlowResultStatus status)
        {
            Status = status;
        }

        public TutorialUiFlowResultStatus Status { get; }
        public bool IsAccepted => Status != TutorialUiFlowResultStatus.Ignored;
        public bool IsCompleted => Status == TutorialUiFlowResultStatus.Completed;

        public static TutorialUiFlowResult Ignored()
        {
            return new TutorialUiFlowResult(TutorialUiFlowResultStatus.Ignored);
        }

        public static TutorialUiFlowResult Stayed()
        {
            return new TutorialUiFlowResult(TutorialUiFlowResultStatus.Stayed);
        }

        public static TutorialUiFlowResult Advanced()
        {
            return new TutorialUiFlowResult(TutorialUiFlowResultStatus.Advanced);
        }

        public static TutorialUiFlowResult Completed()
        {
            return new TutorialUiFlowResult(TutorialUiFlowResultStatus.Completed);
        }
    }
}
