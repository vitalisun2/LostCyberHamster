using Atomic.Elements;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public class EnergyMechanics
    {
        private const int ENERGY_COST_JUMP = 10;
        private const int ENERGY_COST_ROOF_JUMP = 10;
        private const int ENERGY_COST_SUPER_JUMP = 20;
        private const int ENERGY_COST_SUPER_ROOF_JUMP = 20;

        private readonly AtomicVariable<int> _energy;
        private readonly AtomicEvent _jumpRequest;
        private readonly AtomicEvent _roofJumpRequest;
        private readonly AtomicEvent _superJumpRequest;
        private readonly AtomicEvent _superRoofJumpRequest;

        private float _energyTimer = 0f;

        private const int _energyMax = 100;

        public EnergyMechanics(
            AtomicVariable<int> energy,
            AtomicEvent jumpRequest,
            AtomicEvent roofJumpRequest,
            AtomicEvent superJumpRequest,
            AtomicEvent superRoofJumpRequest)
        {
            _energy = energy;
            _jumpRequest = jumpRequest;
            _roofJumpRequest = roofJumpRequest;
            _superJumpRequest = superJumpRequest;
            _superRoofJumpRequest = superRoofJumpRequest;
        }

        public void Subscribe()
        {
            _jumpRequest.Subscribe(OnJump);
            _roofJumpRequest.Subscribe(OnRoofJump);
            _superJumpRequest.Subscribe(OnSuperJump);
            _superRoofJumpRequest.Subscribe(OnSuperRoofJump);
        }

        public void Unsubscribe()
        {
            _jumpRequest.Unsubscribe(OnJump);
            _roofJumpRequest.Unsubscribe(OnRoofJump);
            _superJumpRequest.Unsubscribe(OnSuperJump);
            _superRoofJumpRequest.Unsubscribe(OnSuperRoofJump);
        }

        private void OnJump()
        {
            if (_energy.Value < ENERGY_COST_JUMP) return;

            _energy.Value -= ENERGY_COST_JUMP;
            GameEventsManager.EnergySpent(ENERGY_COST_JUMP);
        }

        private void OnRoofJump()
        {
            if (_energy.Value < ENERGY_COST_ROOF_JUMP) return;

            _energy.Value -= ENERGY_COST_ROOF_JUMP;
            GameEventsManager.EnergySpent(ENERGY_COST_ROOF_JUMP);
        }

        private void OnSuperJump()
        {
            if (_energy.Value < ENERGY_COST_SUPER_JUMP) return;

            _energy.Value -= ENERGY_COST_SUPER_JUMP;
            GameEventsManager.EnergySpent(ENERGY_COST_SUPER_JUMP);
        }

        private void OnSuperRoofJump()
        {
            if (_energy.Value < ENERGY_COST_SUPER_ROOF_JUMP) return;

            _energy.Value -= ENERGY_COST_SUPER_ROOF_JUMP;
            GameEventsManager.EnergySpent(ENERGY_COST_SUPER_ROOF_JUMP);
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
