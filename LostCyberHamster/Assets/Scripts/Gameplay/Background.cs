using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameManagerLogic;
using Atomic.Objects;
using NotImplementedException = System.NotImplementedException;

namespace Assets.Scripts.Gameplay
{
    public class Background : AtomicObject,
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

            _scrollLeftMechanics = new ScrollLeftMechanics(transform);
            _scrollRepeatMechanic = new ScrollRepeatMechanic(transform);
        }

        public void OnStart()
        {
            enabled = true;
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

    }
}
