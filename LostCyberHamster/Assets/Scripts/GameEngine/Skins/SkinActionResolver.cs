using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.GameEngine.Skins
{
    /// <summary>
    /// Преобразует gameplay-состояние Hamster в стабильный visual-контракт.
    /// </summary>
    public static class SkinActionResolver
    {
        /// <summary>
        /// Разрешает gameplay state в action, variant, outcome и имя authoritative transform-клипа.
        /// </summary>
        public static bool TryResolve(HamsterStateEnum state, out SkinActionDescriptor descriptor)
        {
            // Variant и outcome ортогональны семантическому action.
            SkinVisualVariant variant = IsSuper(state)
                ? SkinVisualVariant.Super
                : SkinVisualVariant.Normal;
            SkinVisualOutcome outcome = IsDamage(state)
                ? SkinVisualOutcome.Damage
                : SkinVisualOutcome.Normal;

            // Каждая ветка фиксирует единственный runtime-контракт состояния.
            switch (state)
            {
                case HamsterStateEnum.Run:
                    descriptor = Create(SkinVisualAction.GroundRun, variant, outcome, "transform_default");
                    return true;
                case HamsterStateEnum.RoofRun:
                    descriptor = Create(SkinVisualAction.RoofRun, variant, outcome, "transform_roof_run");
                    return true;
                case HamsterStateEnum.RunFromRoof:
                    descriptor = Create(SkinVisualAction.RunFromRoof, variant, outcome, "transform_run_from_roof");
                    return true;
                case HamsterStateEnum.Jump:
                case HamsterStateEnum.JumpOver:
                case HamsterStateEnum.JumpDamageForSmallNotAlive:
                case HamsterStateEnum.JumpDamageForSmallAlive:
                case HamsterStateEnum.JumpDamageForBigAlive:
                    descriptor = Create(SkinVisualAction.GroundJump, variant, outcome, "transform_jump");
                    return true;
                case HamsterStateEnum.SuperJump:
                case HamsterStateEnum.SuperJumpOver:
                case HamsterStateEnum.SuperJumpDamage:
                    descriptor = Create(SkinVisualAction.GroundJump, variant, outcome, "transform_super_jump");
                    return true;
                case HamsterStateEnum.JumpOnObstacle:
                    descriptor = Create(SkinVisualAction.JumpOnObstacle, variant, outcome, "transform_jump_on");
                    return true;
                case HamsterStateEnum.SuperJumpOnObstacle:
                    descriptor = Create(SkinVisualAction.JumpOnObstacle, variant, outcome, "transform_super_jump_on");
                    return true;
                case HamsterStateEnum.JumpOnRoof:
                case HamsterStateEnum.JumpOnRoofDamage:
                    descriptor = Create(SkinVisualAction.JumpOnRoof, variant, outcome, "transform_jump_on_roof");
                    return true;
                case HamsterStateEnum.SuperJumpOnRoof:
                case HamsterStateEnum.SuperJumpOnRoofDamage:
                    descriptor = Create(SkinVisualAction.JumpOnRoof, variant, outcome, "transform_super_jump_on_roof");
                    return true;
                case HamsterStateEnum.RoofJump:
                case HamsterStateEnum.RoofJumpDamage:
                    descriptor = Create(SkinVisualAction.RoofJump, variant, outcome, "transform_roof_jump");
                    return true;
                case HamsterStateEnum.SuperRoofJump:
                case HamsterStateEnum.SuperRoofJumpDamage:
                    descriptor = Create(SkinVisualAction.RoofJump, variant, outcome, "transform_super_roof_jump");
                    return true;
                case HamsterStateEnum.JumpFromRoof:
                case HamsterStateEnum.JumpFromRoofDamage:
                    descriptor = Create(SkinVisualAction.JumpFromRoof, variant, outcome, "transform_jump_from_roof");
                    return true;
                case HamsterStateEnum.SuperJumpFromRoof:
                case HamsterStateEnum.SuperJumpFromRoofDamage:
                    descriptor = Create(SkinVisualAction.JumpFromRoof, variant, outcome, "transform_super_jump_from_roof");
                    return true;
                case HamsterStateEnum.JumpOnObstacleFromRoof:
                    descriptor = Create(
                        SkinVisualAction.JumpOnObstacleFromRoof,
                        variant,
                        outcome,
                        "transform_jump_on_from_roof");
                    return true;
                case HamsterStateEnum.SuperJumpOnObstacleFromRoof:
                    descriptor = Create(
                        SkinVisualAction.JumpOnObstacleFromRoof,
                        variant,
                        outcome,
                        "transform_super_jump_on_obstacle_from_roof");
                    return true;
                default:
                    descriptor = default;
                    return false;
            }
        }

        private static SkinActionDescriptor Create(
            SkinVisualAction action,
            SkinVisualVariant variant,
            SkinVisualOutcome outcome,
            string transformClipName)
        {
            return new SkinActionDescriptor(action, variant, outcome, transformClipName);
        }

        private static bool IsSuper(HamsterStateEnum state)
        {
            return state is HamsterStateEnum.SuperJump
                or HamsterStateEnum.SuperJumpDamage
                or HamsterStateEnum.SuperJumpOver
                or HamsterStateEnum.SuperJumpOnObstacle
                or HamsterStateEnum.SuperJumpOnRoof
                or HamsterStateEnum.SuperJumpOnRoofDamage
                or HamsterStateEnum.SuperRoofJump
                or HamsterStateEnum.SuperRoofJumpDamage
                or HamsterStateEnum.SuperJumpFromRoof
                or HamsterStateEnum.SuperJumpFromRoofDamage
                or HamsterStateEnum.SuperJumpOnObstacleFromRoof;
        }

        private static bool IsDamage(HamsterStateEnum state)
        {
            return state is HamsterStateEnum.JumpDamageForSmallNotAlive
                or HamsterStateEnum.JumpDamageForSmallAlive
                or HamsterStateEnum.JumpDamageForBigAlive
                or HamsterStateEnum.JumpOnRoofDamage
                or HamsterStateEnum.RoofJumpDamage
                or HamsterStateEnum.JumpFromRoofDamage
                or HamsterStateEnum.SuperJumpDamage
                or HamsterStateEnum.SuperJumpOnRoofDamage
                or HamsterStateEnum.SuperRoofJumpDamage
                or HamsterStateEnum.SuperJumpFromRoofDamage;
        }
    }
}
