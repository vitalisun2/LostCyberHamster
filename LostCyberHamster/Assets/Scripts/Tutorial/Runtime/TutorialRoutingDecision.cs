namespace Assets.Scripts.Tutorial
{
    public readonly struct TutorialRoutingDecision
    {
        private TutorialRoutingDecision(bool shouldRedirect, string targetLevelAddress)
        {
            ShouldRedirect = shouldRedirect;
            TargetLevelAddress = targetLevelAddress;
        }

        public static TutorialRoutingDecision None => new(false, string.Empty);

        public bool ShouldRedirect { get; }

        public string TargetLevelAddress { get; }

        public static TutorialRoutingDecision RedirectTo(string targetLevelAddress)
        {
            return new TutorialRoutingDecision(true, targetLevelAddress);
        }
    }
}
