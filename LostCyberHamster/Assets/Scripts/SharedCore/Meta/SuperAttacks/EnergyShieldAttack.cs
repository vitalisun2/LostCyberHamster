using System;
using System.Collections;
using Assets.Scripts.Common;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
using Assets.Scripts.System.Resources;
using UnityEngine;

namespace Vues.GameCore
{
    /// <summary>
    /// Защищает хомяка и уничтожает препятствия при столкновении ограниченное время.
    /// </summary>
    public sealed class EnergyShieldAttack : ISuperAttackRuntime
    {
        public const string EffectAddress = "EnergyShieldPrefab";
        public const float DefaultDuration = 5f;
        public const int DefaultChargePerObstacle = 20;

        private readonly AddressableLease<GameObject> _effectPrefabLease;
        private readonly GameObject _effectPrefab;
        private readonly float _duration;
        private float _timeLeft;

        /// <summary>
        /// Возвращает заряд за одно уничтоженное препятствие.
        /// </summary>
        public int ChargePerObstacle { get; }

        /// <summary>
        /// Возвращает признак активного щита.
        /// </summary>
        public bool IsActive => _timeLeft > 0f;

        /// <summary>
        /// Создаёт энергетический щит и принимает владение lease prefab эффекта.
        /// </summary>
        public EnergyShieldAttack(
            AddressableLease<GameObject> effectPrefabLease,
            float duration = DefaultDuration,
            int chargePerObstacle = DefaultChargePerObstacle)
        {
            _effectPrefabLease = effectPrefabLease ??
                throw new ArgumentNullException(nameof(effectPrefabLease));
            _effectPrefab = effectPrefabLease.Value ??
                throw new ArgumentException(
                    "Lease не содержит prefab эффекта.",
                    nameof(effectPrefabLease));
            _duration = duration;
            ChargePerObstacle = chargePerObstacle;
        }

        /// <summary>
        /// Активирует щит, если он ещё не действует.
        /// </summary>
        public bool TryActivate()
        {
            if (IsActive)
            {
                Debug.LogWarning("Ulta is already active");
                return false;
            }

            _timeLeft = _duration;
            var hamster = LevelController.Instance.LevelData.Hamster;
            hamster.StartCoroutine(RunAttack(hamster));
            return true;
        }

        /// <summary>
        /// Уменьшает оставшееся время активности щита.
        /// </summary>
        public void Update()
        {
            if (_timeLeft > 0f)
            {
                _timeLeft -= Time.deltaTime;
            }
        }

        /// <summary>
        /// Освобождает lease prefab эффекта.
        /// </summary>
        public void Dispose()
        {
            _effectPrefabLease.Dispose();
        }

        private IEnumerator RunAttack(Hamster hamster)
        {
            hamster.IsProtected.Value = true;
            hamster.IsSuperAttackDestructiveOnCollision.Value = true;

            var attackEffect = HelpMethods.CreateUltaEffect(_effectPrefab, hamster);
            yield return new WaitForSeconds(_duration);

            hamster.IsProtected.Value = false;
            hamster.IsSuperAttackDestructiveOnCollision.Value = false;

            if (attackEffect != null)
            {
                UnityEngine.Object.Destroy(attackEffect);
            }
        }
    }
}
