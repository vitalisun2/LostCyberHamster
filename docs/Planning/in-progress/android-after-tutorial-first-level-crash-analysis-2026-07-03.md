# Android after tutorial first level crash analysis - 2026-07-03

## Scope

Scenario:

1. Complete tutorial on Android.
2. Press `Играть` on final tutorial modal, or return to menu and choose first New York Morning level.
3. Intro starts.
4. Press `Пропустить`.

Expected:

- Intro ends.
- `GameManager.StartGame()` starts normal `01_New_York/Morning/level_01`.
- Game continues without tutorial runtime/sandbox side effects.

Actual:

- Android app exits/crashes around the transition from intro to gameplay.

## Authoritative expected source

User report: before tutorial feature, normal first level + intro skip worked. Tutorial must be an overlay/routing feature and must not break ordinary first level startup after tutorial completion.

## Android device log facts

Fresh collector session:

- Log root: `DeviceLogs/android`.
- Build label: `tutorial-android-final-lan-logs`.
- Device: `Xiaomi M2012K11AG`.
- Last fresh session: `2026-07-03T08-46-22-6053010Z_Xiaomi_M2012K11AG_scene_loaded_Game_2afa1e8f9c78`.
- Metadata:
  - `activeScene = Game`
  - `currentLevel = 01_New_York/Morning/level_01`
  - `sessionStartedAtUtc = 2026-07-03T08:46:15.8901020Z`
- Diagnostic tail:
  - `11:46:18.394 scene loaded name=Menu`
  - `11:46:22.604 scene loaded name=Game`
  - `11:46:22.604 upload queued reason=scene_loaded_Game`
  - `11:46:22.604 upload started reason=scene_loaded_Game`
  - No subsequent `health probe reason=scene_loaded_Game`.
  - No `runtime_error` upload for this session.

Earlier same-session tutorial completion path:

- `11:45:59.130 [TUTORIAL] completed lives=3`
- `11:45:59.195 upload completed reason=tutorial_completed responseCode=200`
- `11:46:01.165 [TUTORIAL SANDBOX] restored real state`
- `11:46:01.197 scene loaded name=Game`
- Metadata for this upload: `currentLevel = 01_New_York/Morning/level_01`
- No subsequent upload completion for that `scene_loaded_Game`.

Interpretation:

- Network/log collector works: previous uploads in the same run complete with HTTP 200.
- Crash/exit happens after Game scene load of real `level_01`.
- The last uploaded snapshot is before the next successful health probe, so the failure may occur very early in Game scene startup or immediately when gameplay starts after intro skip.

## Code facts gathered

### Tutorial completion path

`TutorialGameController.StartFirstGameplayLevel()`:

- `TutorialMetaCoordinator.RestoreSandbox(markTutorialCompleted: true)`
- `TutorialLaunchState.AllowFirstGameplayLevelOnce()`
- `GameDataManager.PlayerData.CurrentLevel = TutorialConstants.FirstGameplayLevelAddress`
- `GameDataManager.SaveData()`
- `SceneManager.LoadScene("Game")`

`TutorialSandboxState.RestoreRealState(true)`:

- Restores money/crystals/skin/current level from snapshot.
- Sets `IsTutorialCompleted = snapshot.IsTutorialCompleted || true`.
- Saves data.
- Sets `IsActive = false`.

Then `StartFirstGameplayLevel()` overwrites current level to real first level and saves.

### Normal first level routing

`TutorialLevelRoutingLoadingTask.LoadAsync()` calls `TutorialLaunchState.RedirectFirstLevelToTutorialIfNeeded()`.

When `IsTutorialCompleted == true`, `ShouldRedirectToTutorial()` returns false for `01_New_York/Morning/level_01`, so the normal first level is not redirected.

### Intro skip path

`Intro.SkipIntro()`:

- Stops intro coroutine.
- Calls `EndIntro()`.

`Intro.EndIntro()`:

- Removes intro UI.
- If `GameManager.State == INTRO`, calls `GameManager.StartGame()`.
- Releases intro sprites.

`GameManager.StartGame()`:

- Iterates `_listeners`.
- Calls every `IGameStartListener.OnStart()`.
- Sets `TimeScaleCoefficient = 1f`.
- Sets state to `PLAYING`.

### Candidate tutorial side effects

- `TutorialRuntimeHost` is `DontDestroyOnLoad`, but on non-tutorial level with inactive meta/sandbox it restores external input and destroys itself in `Update()`.
- `TutorialUiRuntime` remains statically activated after skin lesson, but with `Stage = Completed` it only clears active surface in `Tick()`.
- `TutorialSandboxState` changes `PlayerData` during tutorial but restores and saves real state before loading normal level.

## Current hypotheses

### H1 - crash during Game scene startup before intro

Supported by latest logs: `scene_loaded_Game` upload starts but never reaches health-probe completion.

Need proof:

- Add/read diagnostics around Game loading tasks and `GameEntryPoint.Start`.
- Reproduce in Unity or Android build.

### H2 - crash on `Intro.SkipIntro -> GameManager.StartGame`

Supported by user-visible report: intro appears, skip is pressed, then app exits.

Need proof:

- Add/read diagnostics before/after `Intro.EndIntro`, before/after each `IGameStartListener.OnStart`.
- Reproduce in Unity or Android build.

### H3 - tutorial leaves stale runtime state/subscription

Supported by regression timing: before tutorial feature first level worked.

Candidate areas:

- persistent `TutorialRuntimeHost`;
- static `TutorialUiRuntime`;
- tutorial sandbox state;
- static `Skin` runtime state after tutorial super hit.

Need proof:

- Log tutorial cleanup state before normal first level starts.
- Confirm whether failure disappears if cleanup is explicit before `SceneManager.LoadScene("Game")`.

## Excluded / weaker alternatives

- Collector/network issue: excluded for this run because previous uploads completed with HTTP 200 seconds before the crash.
- Tutorial redirect loop: currentLevel in metadata is real `01_New_York/Morning/level_01`, not `Tutorial Level`.
- Android cleartext HTTP: excluded because same endpoint accepted fresh uploads.

## Unity editor probe facts

Probe command:

- `probe_first_level_intro`
- Forced `01_New_York/Morning/level_01`
- Bypassed tutorial redirect once via `TutorialLaunchState.AllowFirstGameplayLevelOnce()`
- Intro was not auto-skipped by loading settings; probe invoked `SkipIntro()` after intro initialization.

Result:

- `WIN`
- `[INTRO] initialize completed images=5`
- `[INTRO] end begin`
- `[INTRO] before start game gmState=INTRO`
- `[GAME START] begin state=INTRO listeners=1 updateListeners=0 lateUpdateListeners=0`
- `[GAME START] completed state=PLAYING`

Interpretation:

- The normal first level pipeline can complete in Unity Editor after tutorial routing is bypassed.
- Current evidence points to an Android-only failure or to a state/device/build difference not covered by the editor probe.
- Do not patch tutorial/game loading based only on hypotheses; collect the Android stack from the diagnostic APK next.

Second probe after restoring `Assets/AddressableAssetsData/link.xml` and adding crash-tail persistence:

- `WIN`
- `[GAME START] completed state=PLAYING`

## New facts from Android diagnostic attempt 1

Fresh Android upload:

- Build label: `tutorial-android-crash-diagnostics-2026-07-03`
- `scene_loaded_Game` upload was received.
- Log reaches `[DEVICE LOG] scene loaded name=Game mode=Single`.
- No `[GAME ENTRY] start` appears before the received snapshot.
- No `runtime_exception` or `runtime_error` upload arrives after that.

Interpretation:

- The app reaches the Unity Game scene, but the received upload is taken too early to include the crash tail.
- `DebugManager` used to delete `diagnostic_log.txt` on each new startup. That means a crash after `scene_loaded_Game` but before the next upload could be lost on the next app launch.
- `Assets/AddressableAssetsData/link.xml` was missing from the dirty worktree and therefore from the diagnostic APK. This is a plausible Android-only root cause because the file preserves Addressables, UI Toolkit serialized data, sprites, physics, animation, and prefab/component types for IL2CPP stripping. Editor is unaffected by this failure mode.

Action taken for diagnostic attempt 2:

- Restored `Assets/AddressableAssetsData/link.xml` and `.meta`.
- Restored the `Assembly-CSharp.csproj` include for the link file.
- Changed diagnostic log startup to preserve the previous log and trim only if it grows beyond the cap.
- Added scene first-frame and one-second checkpoints.
- Added early `Awake`/`OnEnable` diagnostics around Game scene entry points.

## Next diagnostic step

Add minimal durable diagnostics around:

- `GameEntryPoint.ExecuteTask` start/complete/fail for each loading task.
- `Intro.SkipIntro` and `Intro.EndIntro` before/after `GameManager.StartGame`.
- `GameManager.StartGame` listener names before/after each `OnStart`.

This should distinguish early Game scene startup crash from intro-skip/gameplay-start crash and identify the first failing listener/task.

## New facts from Android diagnostic attempt 2

Fresh Android upload:

- Build label: `tutorial-android-crash-diagnostics-linkxml-2026-07-03`.
- The app reaches `GameEntryPoint.Start`.
- `GameEntryPoint` runs the full first-level loading pipeline and logs `pipeline completed`.
- `GameEntryPoint` completes with `gmState=INTRO`.
- Immediately after that the app queues `application_paused` and `application_quit`, then a new startup begins.

Fatal log evidence:

- Multiple errors are captured from `Assets.Scripts.Common.HelpMethods:GetClipRootYAtHalf`.
- The missing clips are medium roof animation clips, for example:
  - `transform_medium_jump_from_roof`
  - `transform_medium_roof_jump`
  - `transform_medium_run_from_roof`
  - `transform_medium_super_roof_jump`
- Stack:
  - `HelpMethods.LogAndStopGame`
  - `HelpMethods.GetClipRootYAtHalf`
  - `BotAnimationTravelProvider.TryGetRootYAtHalf`
  - `BotAnimationTravelProvider.PrewarmKnownClipData`
  - `RuntimeBotController.TryResolveRuntimeDependencies`

Root cause:

- `HelpMethods.LogAndStopGame` calls `Application.Quit()` in Android builds.
- `RuntimeBotController` auto-starts in the normal first level.
- Bot prewarm asks for root-Y data for optional medium roof clips.
- Those medium roof clips are editor-only today; `TransformAnimatorController.TryFindClip` can load them through `AssetDatabase` in Editor, but returns `false` in builds.
- Therefore the normal Android level quits during bot prewarm, even though the level loading pipeline itself succeeds.

Fix:

- Keep `HelpMethods.GetClipRootYAtHalf` fatal for real gameplay/mechanics callers.
- Change `BotAnimationTravelProvider.TryGetRootYAtHalf` to use a bot-local safe sampler:
  - resolve `TransformAnimatorController`;
  - call `TryFindClip`;
  - if the clip is unavailable in the build, log a bot runtime-safety diagnostic and return `false`;
  - cache only successful samples;
  - remember missing clips to avoid repeated logs.

Why this is scoped:

- No changes to the game loading architecture.
- No global weakening of fatal validation.
- Only the bot cache/prewarm path is made tolerant to optional build-unavailable data.

## Validation after fix

- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal`: succeeded, warnings only.
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:minimal`: succeeded, warnings only.
- Unity editor probe `probe_first_level_intro`: `WIN`; first level intro skip reaches `[GAME START] completed state=PLAYING`.

Android build sent:

- Output: `Builds/telegram-buffer/2026-07-03_12-44-48_integration_unity-live_b79602d1/LostCyberHamster.apk`.
- Development build: `true`.
- APK size: `91046532` bytes.
- Telegram channel: `LostCyberHamster builds`.
- Telegram message id: `25`.
- Build label: `tutorial-android-crash-diagnostics-bot-prewarm-2026-07-03`.
