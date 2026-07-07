# Paris Morning Level 01 Bot Regression Run - 2026-07-07

Level: `02_Paris/Morning/level_01`
Runner: Unity `TestLevelAutomationBridge`
Branch: `integration/unity-live`
Result: `WIN`, 3 stars

## Fallback Check

- Paris obstacle sprites: fallback to `New York obstacles sprites` confirmed.
- Paris obstacle animations: fallback to `New York obstacle animations` confirmed.
- Paris decor sprites: fallback to `New York decor sprites` confirmed.
- Fresh run had no `InvalidKeyException` / `No Location found` Addressables errors after fallback fix.

## Bot Run Summary

- Start: `[GAME START] completed state=PLAYING`
- Finish: `[TEST FINISH] state=FINISHED lives=3 energy=44`
- Energy: start `100`, minimum observed `32`, finish `44`
- Energy pickups observed: `+17`, `+30`, `+30`, `+30`

## Regression Findings

- Energy hunger life loss: not reproduced. Lives stayed at `3`; no fail after minimum energy `32`.
- No valid action window / dead-end: not reproduced. No `[DEAD_END]` entries in diagnostic log.
- Other gameplay regressions: not observed in this run.

## Notes

- Missing intro sprites are still reported for `02_Paris/Morning/level_01`, but the level proceeds and completes; this was not a gameplay regression for this run.
- Diagnostic categories used for this automation run: `TestResult`, `RuntimeSafety`, `DeadEnd`, `Economy`.
