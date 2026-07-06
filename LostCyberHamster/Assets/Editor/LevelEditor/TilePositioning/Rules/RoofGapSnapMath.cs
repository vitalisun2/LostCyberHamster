using Assets.Scripts;

/// <summary>
/// Считает целевые roof-to-roof gaps для нормализации спорного диапазона.
/// </summary>
internal static class RoofGapSnapMath
{
    private const float TightRoofGapFactor = 0.3f;

    public static bool TryGetTargetGap(
        float currentGap,
        float hamsterWidth,
        out float targetGap,
        out RoofGapSnapTarget target)
    {
        targetGap = 0f;
        target = RoofGapSnapTarget.Tight;

        float tightGap = GetTightGap(hamsterWidth);
        float passiveGap = Consts.GetRoofRunPassiveContinuationGap(hamsterWidth);
        if (currentGap <= tightGap || currentGap >= passiveGap)
        {
            return false;
        }

        float midpointGap = (tightGap + passiveGap) * 0.5f;
        if (currentGap <= midpointGap)
        {
            targetGap = tightGap;
            target = RoofGapSnapTarget.Tight;
            return true;
        }

        targetGap = passiveGap + Consts.GridSnapStep;
        target = RoofGapSnapTarget.Wide;
        return true;
    }

    public static float GetTightGap(float hamsterWidth)
    {
        return hamsterWidth * TightRoofGapFactor;
    }
}
