namespace Assets.Scripts.Tutorial
{
    /// <summary>
    /// Не даёт одному UI-переходу выполниться повторно до следующей presentation.
    /// </summary>
    public sealed class TutorialTransitionGuard
    {
        private bool _started;

        public bool TryBegin()
        {
            if (_started)
            {
                return false;
            }

            _started = true;
            return true;
        }

        public void Reset()
        {
            _started = false;
        }
    }
}
