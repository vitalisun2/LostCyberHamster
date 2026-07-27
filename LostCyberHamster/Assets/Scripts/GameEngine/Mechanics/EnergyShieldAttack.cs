using System.Collections;
using Assets.Scripts.Common;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public sealed class EnergyShieldAttack
    {
        public const string EffectAddress = "EnergyShieldPrefab";
        public const float DefaultDuration = 5f;
        public const int DefaultChargePerObstacle = 20;

        private readonly GameObject _effectPrefab;
        private readonly float _duration;
        private float _timeLeft;

        public int ChargePerObstacle { get; }
        public bool IsActive => _timeLeft > 0f;

        public EnergyShieldAttack(
            GameObject effectPrefab,
            float duration = DefaultDuration,
            int chargePerObstacle = DefaultChargePerObstacle)
        {
            _effectPrefab = effectPrefab;
            _duration = duration;
            ChargePerObstacle = chargePerObstacle;
        }

        public void Apply()
        {
            if (IsActive)
            {
                Debug.LogWarning("Ulta is already active");
                return;
            }

            _timeLeft = _duration;
            var hamster = LevelController.Instance.LevelData.Hamster;
            hamster.StartCoroutine(RunAttack(hamster));
        }

        public void Update()
        {
            if (_timeLeft > 0f)
            {
                _timeLeft -= Time.deltaTime;
            }
        }

        private IEnumerator RunAttack(Hamster hamster)
        {
            hamster.IsProtected.Value = true;
            hamster.IsDestructiveOnCollision.Value = true;

            var attackEffect = HelpMethods.CreateUltaEffect(_effectPrefab, hamster);
            yield return new WaitForSeconds(_duration);

            hamster.IsProtected.Value = false;
            hamster.IsDestructiveOnCollision.Value = false;

            if (attackEffect != null)
            {
                Object.Destroy(attackEffect);
            }
        }
    }
}
