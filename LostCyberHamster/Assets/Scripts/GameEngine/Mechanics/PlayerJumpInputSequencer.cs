using System;
using Assets.Scripts.Common;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public sealed class PlayerJumpInputSequencer
    {
        private const int JumpEnergyCost = 10;

        private readonly Hamster _character;
        private readonly DoubleJumpDetector _skateboardDoubleJumpDetector = new();
        private bool _isJumpUpgradeArmed;
        private float _jumpUpgradeExpireTime;
        private bool _wasSkateboardActive;

        public PlayerJumpInputSequencer(Hamster character)
        {
            _character = character ?? throw new ArgumentNullException(nameof(character));
            _wasSkateboardActive = character.ActorSwitcher.IsSkateboardActive;
        }

        public void HandleJumpInput()
        {
            RefreshState();

            if (_character.ActorSwitcher.IsSkateboardActive)
            {
                HandleSkateboardJumpInput();
                return;
            }

            if (TryUpgradeCurrentJump())
                return;

            if (TryStartJump())
                ArmJumpUpgradeWindow();
        }

        public void Reset()
        {
            _skateboardDoubleJumpDetector.Reset();
            ResetRegularJumpUpgradeWindow();
            _wasSkateboardActive = _character.ActorSwitcher.IsSkateboardActive;
        }

        private void RefreshState()
        {
            bool isSkateboardActive = _character.ActorSwitcher.IsSkateboardActive;
            if (isSkateboardActive != _wasSkateboardActive)
            {
                Reset();
                return;
            }

            if (_isJumpUpgradeArmed && Time.time > _jumpUpgradeExpireTime)
                ResetRegularJumpUpgradeWindow();
        }

        private void HandleSkateboardJumpInput()
        {
            // Skateboard хранит отдельную ride/landing queue логику за теми же request-событиями.
            bool isDoubleJump = _skateboardDoubleJumpDetector.RegisterJump();
            if (isDoubleJump)
                _character.SuperJumpRequest.Invoke();
            else
                _character.JumpRequest.Invoke();
        }

        private bool TryStartJump()
        {
            if (_character.Energy.Value < JumpEnergyCost)
                return false;

            HamsterStateEnum hamsterState = _character.HamsterState.Value;
            if (hamsterState == HamsterStateEnum.RoofRun)
            {
                _character.RoofJumpRequest.Invoke();
                return true;
            }

            if (hamsterState == HamsterStateEnum.Run || _character.IsDamaged.Value)
            {
                _character.JumpRequest.Invoke();
                return true;
            }

            return false;
        }

        private bool TryUpgradeCurrentJump()
        {
            if (!_isJumpUpgradeArmed)
                return false;

            HamsterStateEnum hamsterState = _character.HamsterState.Value;
            if (CanUpgradeToSuperRoofJump(hamsterState))
            {
                _character.SuperRoofJumpRequest.Invoke();
                ResetRegularJumpUpgradeWindow();
                return true;
            }

            if (CanUpgradeToSuperJump(hamsterState))
            {
                _character.SuperJumpRequest.Invoke();
                ResetRegularJumpUpgradeWindow();
                return true;
            }

            if (!IsRegularJumpUpgradeState(hamsterState))
                ResetRegularJumpUpgradeWindow();

            return false;
        }

        private void ArmJumpUpgradeWindow()
        {
            _isJumpUpgradeArmed = true;
            _jumpUpgradeExpireTime = Time.time + DoubleJumpDetector.DoubleJumpThreshold;
        }

        private void ResetRegularJumpUpgradeWindow()
        {
            _isJumpUpgradeArmed = false;
            _jumpUpgradeExpireTime = 0f;
        }

        private static bool IsRegularJumpUpgradeState(HamsterStateEnum hamsterState)
        {
            return CanUpgradeToSuperJump(hamsterState) || CanUpgradeToSuperRoofJump(hamsterState);
        }

        private static bool CanUpgradeToSuperJump(HamsterStateEnum hamsterState)
        {
            return hamsterState == HamsterStateEnum.Jump
                   || hamsterState == HamsterStateEnum.JumpOver
                   || hamsterState == HamsterStateEnum.JumpOnObstacle
                   || hamsterState == HamsterStateEnum.JumpOnRoof
                   || hamsterState == HamsterStateEnum.JumpDamageForSmallAlive
                   || hamsterState == HamsterStateEnum.JumpDamageForSmallNotAlive
                   || hamsterState == HamsterStateEnum.JumpDamageForBigAlive
                   || hamsterState == HamsterStateEnum.JumpOnRoofDamage;
        }

        private static bool CanUpgradeToSuperRoofJump(HamsterStateEnum hamsterState)
        {
            return hamsterState == HamsterStateEnum.RoofJump
                   || hamsterState == HamsterStateEnum.RoofJumpDamage
                   || hamsterState == HamsterStateEnum.JumpFromRoof
                   || hamsterState == HamsterStateEnum.JumpFromRoofDamage
                   || hamsterState == HamsterStateEnum.JumpOnObstacleFromRoof;
        }
    }
}