namespace LostCyberHamster.UI
{
    public interface IScreenController
    {
        public ScreenEnum Type { get; }
        public void SubscribeToEvents();
        public void UnsubscribeFromEvents();
    }

}
