using Atomic.Elements;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public class EnergyMechanics
    {
        private readonly AtomicVariable<int> _energy;
        private readonly AtomicEvent _jumpRequest;
        private float _energyTimer = 0f;

        private int _energyToJump = 10;
        private const int _energyMax = 100;

        public EnergyMechanics(AtomicVariable<int> energy, AtomicEvent jumpRequest)
        {
            _energy = energy;
            _jumpRequest = jumpRequest;
        }

        public void Subscribe()
        {
            _jumpRequest.Subscribe(OnJump);
        }

        public void Unsubscribe()
        {
            _jumpRequest.Unsubscribe(OnJump);
        }

        private void OnJump()
        {
            if (_energy.Value >= _energyToJump)
            {
                _energy.Value -= _energyToJump;
                GameEventsManager.EnergySpent(_energyToJump);
            }
        }

        public void OnUpdate(float deltaTime)
        {
            RestoreEnergy(deltaTime);
        }

        private void RestoreEnergy(float deltaTime)
        {
            if(_energy.Value >= _energyMax)
            {
                return;
            }

            _energyTimer += deltaTime;
            if (_energyTimer >= 1f)
            {
                _energy.Value += 1;
                _energyTimer = 0f;
            }
        }

    }
}
