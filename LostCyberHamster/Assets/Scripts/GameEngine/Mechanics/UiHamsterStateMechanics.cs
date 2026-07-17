using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
using LostCyberHamster.UI;
using UnityEngine.UIElements;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public class UiHamsterStateMechanics
    {
        private readonly Label _debugText;
        private readonly Label _scoreBar;
        private readonly Energybar _energyBar;
        private readonly Hamster _character;

        public UiHamsterStateMechanics(Label debugText, Label scoreBar, Energybar energyBar, Hamster character)
        {
            _debugText = debugText;
            _scoreBar = scoreBar;
            _energyBar = energyBar;
            _character = character;
        }

        public void OnUpdate()
        {
            _debugText.text = _character.HamsterState.Value.ToString();
            UpdateEnergyBar(_character.Energy.Value);
        }

        private void UpdateEnergyBar(int energyValue)
        {
            _energyBar.value = energyValue;
        }
    }
}
