using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameManagerLogic;
using Atomic.Objects;

namespace Assets.Scripts.Gameplay
{
    public class Road : AtomicObject,
        Listeners.IGameStartListener,
        Listeners.IGameUpdateListener,
        Listeners.IGamePauseListener,
        Listeners.IGameResumeListener
    {
        private ScrollLeftMechanics _scrollLeftMechanics;
        private ScrollRepeatMechanic _scrollRepeatMechanic;

        public void Awake()
        {
            enabled = false;

            _scrollLeftMechanics = new ScrollLeftMechanics(transform, Consts.RoadScrollSpeed);
            _scrollRepeatMechanic = new ScrollRepeatMechanic(transform);
        }

        public void OnStart()
        {
            enabled = true;
            RefreshScrollBounds();
        }

        public void OnUpdate(float deltaTime)
        {
            _scrollLeftMechanics.Update(deltaTime);
            _scrollRepeatMechanic.Update();
        }

        public void OnPause()
        {
            enabled = false;
        }

        public void OnResume()
        {
            enabled = true;
        }

        // Addressable sprites are plugged in after Awake; refresh bounds once the real sprite is present.
        public void RefreshScrollBounds()
        {
            _scrollRepeatMechanic?.RefreshBounds();
        }

    }
}
