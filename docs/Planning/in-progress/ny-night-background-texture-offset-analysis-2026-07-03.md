# New York Night background texture offset analysis - 2026-07-03

## Scope

- Regression: New York / Night campaign levels render the level/background texture stack with the main background visually shifted down, leaving a visible blue horizontal gap.
- Affected case from user: New York Night levels, example screenshot label `New York Night, easy_run_2 0`.
- Expected: Night background layers align continuously with the playable level background, without an exposed solid-blue band between skyline/water and foreground.
- Actual: skyline/water layer appears high, foreground/playable background appears lower, and a blue strip is visible between them.

## Workflow

- Prompt: `.github/prompts/manual-stages/bug-regression-workflow.prompt.md`.
- Constraint: prove root cause only; do not fix code.

## Evidence Log

| Step | Source / Command | Fact |
|---|---|---|
| Initial | User screenshot `Photo 1.jpg` | New York Night runtime view shows a solid-blue band between distant skyline/water and foreground level texture. |
| Expected contract clarification | User follow-up | Variable texture height is allowed. The invariant is stable lower border / bottom anchor, not fixed `240px` height. |
| Runtime placement constants | `Consts.cs` | `BackgroundYPos = 0.515`, `Background2YPos = BackgroundYPos + 0.885`, `SkyYPos = BackgroundYPos + ENVIRONMENT_REFERENCE_HEIGHT * PIXELS_TO_UNITS_RATIO / 1.8`, `RoadYPos = BackgroundYPos - ROAD_HEIGHT * PIXELS_TO_UNITS_RATIO`. |
| Runtime placement code | `InitBackgroundLoadingTask.cs` | Background objects are instantiated at `new Vector3(..., Consts.BackgroundYPos, 0f)` before assigning sprite; no height-based bottom-anchor compensation is applied. |
| Sprite assignment | `SpriteRendererMaterialHelper.cs` | `ApplySpriteWithDefaultMaterial` only assigns `renderer.sprite = sprite` and material; it does not alter pivot, bounds, local offset, or anchor. |
| Prefab transform | `ScrollingEnvironmentPrefab.prefab` | Root and child `ScrollingEnvironmentSprite` local positions are both `{x:0,y:0,z:0}`; no prefab-level vertical offset compensates sprite height. |
| New York background sizes | PNG IHDR inspection | `bg_new_york_morning/afternoon/evening.png` are `2000x240`; `bg_new_york_night.png` is `2000x1000`. |
| New York background import settings | `.meta` inspection | `bg_new_york_morning.png` and `bg_new_york_night.png` both use `spritePivot: {x:0.5,y:0.5}` and `spritePixelsToUnits: 100`. |
| Bottom anchor calculation | Current constants + PNG sizes | With center pivot, Morning/Afternoon/Evening actual bottom is `-0.685`, matching the reference seam. Night actual bottom is `-4.485`; to keep the same bottom seam, Night center would need to be `4.315`, not `0.515`. |
| Variable-height commit | `git show fba9f3b9` | Commit `Allow variable height backgrounds` removed fixed height validation but did not change runtime `InitBackgroundLoadingTask` anchoring. |

## Hypotheses

- H1: Night-specific level data points to a different/misconfigured environment texture set.
- H2: Runtime background placement code applies a night/day-part-specific offset.
- H3: Imported Night texture dimensions/pivot/metadata differ from other day parts, causing shared placement code to expose a gap.
- H4: Camera or UI aspect scaling shifted globally, but only visible in Night.

## Current Conclusion

- `bg_new_york_night.png` being taller is not itself invalid under the clarified contract.
- The failing invariant is bottom anchoring: runtime treats `Consts.BackgroundYPos` as sprite pivot/center position because the sprite pivot is center and no code compensates by `sprite.bounds.size.y`.
- Morning/Afternoon/Evening look correct because their `240px` height equals `ENVIRONMENT_REFERENCE_HEIGHT`, so the reference center position accidentally produces the intended bottom seam.
- Night exposes the missing bottom-anchor behavior because its height is `1000px`.
