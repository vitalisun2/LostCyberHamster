using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameManagerLogic;
using Atomic.Objects;
using NotImplementedException = System.NotImplementedException;

namespace Assets.Scripts.Gameplay
{
    public class ScrollingEnvironment : AtomicObject,
        Listeners.IGameStartListener,
        Listeners.IGameUpdateListener,
        Listeners.IGamePauseListener,
        Listeners.IGameResumeListener
    {
        public float ScrollSpeed = Consts.BackgroundScrollSpeed;

        private ScrollLeftMechanics _scrollLeftMechanics;
        private ScrollRepeatMechanic _scrollRepeatMechanic;

        public void Awake()
        {
            enabled = false;
            _scrollRepeatMechanic = new ScrollRepeatMechanic(transform);
        }

        public void OnStart()
        {
            enabled = true;
            
            // Initialize scroll mechanics with the configured speed
            if (_scrollLeftMechanics == null)
            {
                _scrollLeftMechanics = new ScrollLeftMechanics(transform, ScrollSpeed);
            }
            
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
