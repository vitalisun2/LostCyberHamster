namespace Assets.Scripts.Tutorial
{
    public sealed class TutorialUiActionContext
    {
        public static readonly TutorialUiActionContext Empty = new(-1);

        private TutorialUiActionContext(int skinId)
        {
            SkinId = skinId;
        }

        public int SkinId { get; }
        public bool HasSkin => SkinId >= 0;

        public static TutorialUiActionContext ForSkin(int skinId)
        {
            return new TutorialUiActionContext(skinId);
        }
    }
}
