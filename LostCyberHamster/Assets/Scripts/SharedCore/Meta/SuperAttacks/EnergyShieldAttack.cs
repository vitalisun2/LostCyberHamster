using System;
using Assets.Scripts.Common;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System.Resources;
using UnityEngine;

namespace Vues.GameCore
{
    /// <summary>Владеет единым временем защиты, разрушением уровней II/III и бюджетом дропа.</summary>
    public sealed class EnergyShieldAttack : ISuperAttackRuntime
    {
        public const string EffectAddress = "EnergyShieldPrefab";
        public const float DefaultDuration = 3f;
        public const int DefaultChargePerObstacle = 20;
        private readonly AddressableLease<GameObject> _lease;
        private readonly SuperAttackData _data;
        private readonly Hamster _hamster;
        private readonly GameManager _gameManager;
        private SuperAttackLevelData _level;
        private SuperAttackDropBudget _drops;
        private GameObject _effect;
        private float _remaining;
        private long _activationId;
        private bool _disposed;

        public int ChargePerObstacle => _data.UltaCharge;
        public bool IsActive => _remaining > 0;
        public SuperAttackRuntimeSnapshot Snapshot => new(_data.Id, _level?.Level ?? 1, _activationId,
            IsActive, _remaining, _level?.Duration ?? 0, destroyedCount: _drops?.DestroyedCount ?? 0,
            dropsCreated: _drops?.DropsCreated ?? 0);

        public EnergyShieldAttack(AddressableLease<GameObject> lease, SuperAttackData data,
            Hamster hamster, GameManager gameManager)
        {
            _lease = lease ?? throw new ArgumentNullException(nameof(lease));
            if (_lease.Value == null) throw new ArgumentException("Shield effect prefab is missing.", nameof(lease));
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _hamster = hamster ?? throw new ArgumentNullException(nameof(hamster));
            _gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
            _gameManager.OnFinish += End;
        }

        public bool TryActivate()
        {
            if (_disposed || IsActive || _gameManager.State != GameState.PLAYING) return false;
            // Параметры фиксируются на активацию; UI и защита читают один lifecycle.
            _level = SuperAttackLevelResolver.GetEffective(_data);
            _effect = HelpMethods.CreateUltaEffect(_lease.Value, _hamster);
            _drops = new SuperAttackDropBudget(_level);
            _activationId++;
            _remaining = _level.Duration;
            _hamster.IsProtected.Value = true;
            _hamster.IsSuperAttackDestructiveOnCollision.Value = _level.DestroysOnCollision;
            return true;
        }

        public void Update()
        {
            if (_disposed || !IsActive || _gameManager.State != GameState.PLAYING) return;
            _remaining = Mathf.Max(0, _remaining - Time.deltaTime);
            if (_remaining <= 0) End();
        }

        public void OnObstacleDestroyed(Obstacle obstacle)
        {
            if (IsActive && _level.DestroysOnCollision && _drops.TryCreateDrop(obstacle))
                _hamster.ObstacleBonusDropEvent.Invoke(obstacle);
        }

        private void End()
        {
            _remaining = 0;
            _hamster.IsProtected.Value = false;
            _hamster.IsSuperAttackDestructiveOnCollision.Value = false;
            if (_effect != null) UnityEngine.Object.Destroy(_effect);
            _effect = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _gameManager.OnFinish -= End;
            End();
            _lease.Dispose();
        }
    }
}
