using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Scripts.Gameplay;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Online;
using Assets.Scripts.System;
using Assets.Scripts.Tutorial;
using GameManagement;
using GameManagement.Progress;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vues.GameCore;

namespace Assets.Scripts.Diagnostics
{
    /// <summary>Наблюдает подтверждённые изменения и lifecycle тестового прогона; ошибки журнала изолированы от игры.</summary>
    public sealed class EconomyTelemetry : MonoBehaviour
    {
        private static EconomyTelemetry _instance;
        private EconomyJournal _journal;
        private EconomySnapshot _last;
        private string _profile, _run, _level, _session;
        private string _cohort, _runCloseHint;
        private double _active, _nextProgress;
        private bool _paused, _focused = true;
        private EconomyProgressionState _progressionBefore;
        private GameManager _game;
        private Hamster _hamster;
        private readonly List<EconomyFlow> _pending = new();
        private DeviceLogUploadSettings _settings;

        public static bool Enabled => _instance != null && _instance._journal != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            var settings = DeviceLogUploader.LoadSettings();
            // В Editor сохраняем локальный журнал для диагностики; загрузка отдельно остаётся запрещена allowInEditor=false.
            var telemetryAllowed = Application.isEditor || settings?.IsPlatformAllowed() == true;
            if (!(Application.isEditor || Debug.isDebugBuild) || settings?.economyTelemetryEnabled != true ||
                !telemetryAllowed || _instance != null) return;
            var host = new GameObject(nameof(EconomyTelemetry));
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<EconomyTelemetry>();
        }

        private void Awake()
        {
            _settings = DeviceLogUploader.LoadSettings();
            _session = Guid.NewGuid().ToString("N");
            Safe(() => _journal = new EconomyJournal(Path.Combine(Application.persistentDataPath, "economy"), _settings.endpointUrl));
            GameDataManager.ProfileChanged += ProfileChanged;
            GameDataManager.ProgressReconciled += ProgressReconciled;
            GameDataManager.SaveFailed += SaveFailed;
            GameEventsManager.OnLevelStarted += StartRun;
            SceneManager.sceneUnloaded += SceneUnloaded;
            Application.quitting += HandleApplicationQuitting;
            if (GameDataManager.IsLoaded) ProfileChanged();
        }

        private void ProfileChanged() => Safe(() =>
        {
            if (!GameDataManager.IsLoaded || _journal == null) return;
            if (_profile == GameDataManager.ProfileId && _last != null)
            {
                var replacement = EconomySnapshot.Capture(GameDataManager.PlayerData);
                if (JsonUtility.ToJson(_last) != JsonUtility.ToJson(replacement))
                {
                    Emit(new EconomyEvent { type = "data_gap", source = "profile_replaced", before = _last, after = replacement });
                    _last = replacement;
                    _pending.Clear();
                    PersistState();
                }
                return;
            }
            if (!string.IsNullOrEmpty(_run)) FinishInternal("profile_changed", 0, false);
            _profile = GameDataManager.ProfileId;
            _cohort = Application.isEditor ? "editor" : GameDataManager.IsProgressionTestingProfile ? "dev" : "playtest";
            if (_journal.State.dev_profiles.Contains(_profile)) _cohort = "dev";
            _pending.Clear();
            _progressionBefore = null;
            _last = EconomySnapshot.Capture(GameDataManager.PlayerData);

            // Сверяем сохранение с последней локальной квитанцией и отмечаем оборванную попытку.
            var previous = _journal.State;
            if (previous.profile == _profile && previous.snapshot != null &&
                JsonUtility.ToJson(previous.snapshot) != JsonUtility.ToJson(_last))
                Emit(new EconomyEvent { type = "data_gap", source = "save_journal_mismatch", before = previous.snapshot, after = _last });
            if (previous.profile == _profile && !string.IsNullOrEmpty(previous.run))
                Emit(new EconomyEvent { type = "run_finished", source = RecoverUnfinishedOutcome(previous.run_close_hint),
                    run_id = previous.run, level = previous.level, active_seconds = previous.active, confirmed = false,
                    remaining_lives = previous.remaining_lives,
                    after = previous.run_snapshot ?? previous.snapshot ?? _last,
                    runtime = CloneRuntimeState(previous.run_runtime, RecoverUnfinishedLootDisposition(previous.run_close_hint)),
                    flows = CloneFlows(previous.pending_flows, allowEmpty: false) });
            Emit(new EconomyEvent { type = "session_started", after = _last, confirmed = true,
                ads_test_mode = GameAds.MonetizationConfig.Current.AdsTestMode,
                purchases_enabled = GameAds.MonetizationConfig.Current.EnablePurchases,
                interstitial_enabled = GameAds.MonetizationConfig.Current.EnableInterstitial });
            PersistState();
        });

        /// <summary>Снимок до транзакции отделяет добычу забега от внешней награды или покупки.</summary>
        public static EconomySnapshot BeforeTransaction()
        {
            EconomySnapshot result = null;
            Safe(() => { if (Ready()) result = EconomySnapshot.Capture(GameDataManager.PlayerData); });
            return result;
        }

        /// <summary>Вызывается после записи, до уведомлений других игровых обработчиков.</summary>
        public static void Committed(CheckpointReason reason, EconomySnapshot before = null) => Safe(() =>
        {
            if (!Ready()) return;
            var self = _instance;
            var after = EconomySnapshot.Capture(GameDataManager.PlayerData);
            string id = self._profile + ":" + GameDataManager.LocalRevision + ":" + reason;
            if (before != null)
            {
                self.Transaction(self._last, before, "gameplay_checkpoint", id + ":ambient", self._pending.ToArray());
                self.Transaction(before, after, reason.ToString(), id, null);
            }
            else self.Transaction(self._last, after, reason.ToString(), id, self._pending.ToArray());
            self._pending.Clear();
            self._last = after;
            self.PersistState();
        });

        private void Transaction(EconomySnapshot before, EconomySnapshot after, string reason, string id, EconomyFlow[] flows)
        {
            if (before == null || after == null || JsonUtility.ToJson(before) == JsonUtility.ToJson(after)) return;
            string detail = string.Join("|", after.receipts.Except(before.receipts).Concat(after.quests.Except(before.quests))
                .Concat(after.return_rewards.Except(before.return_rewards)));
            var item = new EconomyEvent { type = "economy_transaction", source = reason, detail = detail,
                operation_id = id, before = before, after = after, confirmed = true, flows = flows,
                xp_delta = after.TotalXp - before.TotalXp, coins_delta = (long)after.coins - before.coins,
                crystals_delta = (long)after.crystals - before.crystals,
                points_delta = (long)after.development_points - before.development_points };
            if (reason == nameof(CheckpointReason.DeveloperResourceGranted))
            {
                _cohort = "dev";
                if (!_journal.State.dev_profiles.Contains(_profile)) _journal.State.dev_profiles.Add(_profile);
            }
            Emit(item);
        }

        /// <summary>Суммирует фактическую добычу между checkpoint без строки на каждую монету.</summary>
        public static void Collection(string resource, int amount, string source) => Safe(() =>
        {
            if (!Ready() || amount <= 0) return;
            var flow = _instance._pending.FirstOrDefault(x => x.resource == resource && x.source == source);
            if (flow == null) _instance._pending.Add(flow = new EconomyFlow { resource = resource, source = source });
            flow.income += amount;
        });

        public static void Record(string type, string source, string detail = null, int value = 0) => Safe(() =>
        {
            if (Ready(allowTutorial: true)) _instance.Emit(new EconomyEvent { type = type, source = source, detail = detail, value = value });
        });

        /// <summary>Записывает только изменившуюся при загрузке сверку прогресса с каталогом.</summary>
        private void ProgressReconciled(string reason)
        {
            Safe(() =>
            {
                if (!Ready(allowTutorial: true)) return;
                Emit(new EconomyEvent
                {
                    type = "progress_reconciled",
                    source = reason,
                    after = EconomySnapshot.Capture(GameDataManager.PlayerData),
                    progression_after = CaptureProgressionState(GameDataManager.PlayerData.CurrentLevel),
                    confirmed = true
                });
                PersistState();
            });
        }

        /// <summary>Редкие переходы попытки и рекламы: revive, refill и судьба добычи.</summary>
        internal static void RecordRunTransition(
            string action,
            string reason,
            int value = 0,
            Action<EconomyRuntimeState> enrich = null,
            bool allowTutorial = false) => Safe(() =>
        {
            if (!Ready(allowTutorial) || string.IsNullOrWhiteSpace(action))
                return;
            _instance.EmitRunTransition(action, reason, value, enrich);
        });

        /// <summary>Помечает профиль после искусственной подготовки общими DEV/Editor runner-ами.</summary>
        public static void MarkDevelopment(string source) => Safe(() =>
        {
            if (!Ready(allowTutorial: true)) return;
            _instance._cohort = "dev";
            if (!_instance._journal.State.dev_profiles.Contains(_instance._profile))
                _instance._journal.State.dev_profiles.Add(_instance._profile);
            Record("development_action", source);
            _instance.PersistState();
        });

        private void StartRun(int _) => Safe(() =>
        {
            if (!Ready(allowTutorial: true)) return;
            if (!string.IsNullOrEmpty(_run)) FinishInternal("exit", 0, false, -1, "discarded");
            _run = Guid.NewGuid().ToString("N");
            _level = GameDataManager.PlayerData.CurrentLevel;
            _active = 0;
            _runCloseHint = "active";
            _game = FindAnyObjectByType<GameManager>();
            _hamster = FindAnyObjectByType<Hamster>();
            int best = LevelManager.TryGetCurrentProgressKey(out var key) ? GameDataManager.PlayerData.Progress.GetStars(key) : 0;
            _progressionBefore = CaptureProgressionState(_level);
            Emit(new EconomyEvent { type = "run_started", previous_best_stars = best,
                source = best > 0 ? "repeat" : "uncompleted", before = EconomySnapshot.Capture(GameDataManager.PlayerData),
                progression_before = _progressionBefore, runtime = CaptureRuntimeState("pending") });
            PersistState();
        });

        public static void FinishRun(string outcome, int stars = 0, int remainingLives = -1, string lootDisposition = null) => Safe(() =>
        {
            if (Ready(allowTutorial: true))
                _instance.FinishInternal(outcome, stars, outcome == "win" || outcome == "tutorial_completed", remainingLives, lootDisposition);
        });

        private void FinishInternal(string outcome, int stars, bool confirmed, int remainingLives = -1,
            string lootDisposition = null)
        {
            if (string.IsNullOrEmpty(_run)) return;
            var flows = CloneFlows(_pending, allowEmpty: false);
            var runtime = CaptureRuntimeState(lootDisposition);
            var progressionAfter = CaptureProgressionState(_level);
            Emit(new EconomyEvent { type = "run_finished", source = outcome, stars = stars, confirmed = confirmed,
                remaining_lives = remainingLives >= 0 ? remainingLives : CurrentRemainingLives(),
                progression_before = _progressionBefore, progression_after = progressionAfter,
                after = EconomySnapshot.Capture(GameDataManager.PlayerData), runtime = runtime, flows = flows });
            _pending.Clear();
            _run = null;
            _level = null;
            _active = 0;
            _runCloseHint = null;
            _progressionBefore = null;
            _game = null;
            _hamster = null;
            PersistState();
            _journal.RequestUpload();
        }

        private void SceneUnloaded(Scene scene)
        {
            if (scene.name == "Game" || scene.name.IndexOf("tutorial", StringComparison.OrdinalIgnoreCase) >= 0)
                FinishRun(SceneUnloadOutcome());
        }

        private void Update()
        {
            if (_game != null && _game.State == GameState.PLAYING && !_paused && _focused)
                _active += Time.unscaledDeltaTime;
            if (Time.realtimeSinceStartupAsDouble < _nextProgress) return;
            _nextProgress = Time.realtimeSinceStartupAsDouble + 15;
            Safe(() =>
            {
                if (!Ready(allowTutorial: true)) return;
                if (!string.IsNullOrEmpty(_run))
                    Emit(new EconomyEvent { type = "run_progress", source = "unconfirmed", after = EconomySnapshot.Capture(GameDataManager.PlayerData),
                        runtime = CaptureRuntimeState("pending") });
                PersistState();
                _journal.RequestUpload();
            });
        }

        private void OnApplicationPause(bool paused)
        {
            _paused = paused;
            if (!string.IsNullOrEmpty(_run)) _runCloseHint = paused ? "backgrounded" : "active";
            Record("session_activity", paused ? "background" : "resumed");
            Safe(() => { PersistState(); _journal?.RequestUpload(); });
        }

        private void OnApplicationFocus(bool focused) => _focused = focused;
        private void OnApplicationQuit() => HandleApplicationQuitting();
        private void SaveFailed(Exception _) => Record("data_gap", "save_failed");

        private void HandleApplicationQuitting() => Safe(() =>
        {
            if (!string.IsNullOrEmpty(_run))
            {
                _runCloseHint = "quitting";
                FinishInternal("app_closed", 0, false, -1, "discarded");
            }
            PersistState();
            _journal?.RequestUpload();
        });

        private void PersistState()
        {
            if (_journal == null || _last == null) return;
            _journal.State.profile = _profile;
            _journal.State.snapshot = _last;
            _journal.State.run = _run;
            _journal.State.level = _level;
            _journal.State.active = _active;
            _journal.State.cohort = _cohort;
            _journal.State.run_close_hint = _runCloseHint;
            _journal.State.remaining_lives = string.IsNullOrEmpty(_run) ? -1 : CurrentRemainingLives();
            _journal.State.run_snapshot = string.IsNullOrEmpty(_run) ? null : EconomySnapshot.Capture(GameDataManager.PlayerData);
            _journal.State.run_runtime = string.IsNullOrEmpty(_run) ? null : CaptureRuntimeState("pending");
            _journal.State.pending_flows = string.IsNullOrEmpty(_run) ? Array.Empty<EconomyFlow>() : CloneFlows(_pending);
            _journal.SaveState();
        }

        private int CurrentRemainingLives()
        {
            if (_hamster == null) _hamster = FindAnyObjectByType<Hamster>();
            return _hamster != null ? _hamster.Lives.Value : -1;
        }

        private string SceneUnloadOutcome() => _runCloseHint == "backgrounded" || _runCloseHint == "quitting"
            ? "app_closed"
            : "exit";

        private static string RecoverUnfinishedOutcome(string hint) => hint == "backgrounded" || hint == "quitting"
            ? "app_closed"
            : "interrupted";

        private static string RecoverUnfinishedLootDisposition(string hint) => hint == "backgrounded" || hint == "quitting"
            ? "discarded"
            : "discarded";

        /// <summary>Собирает редкий снимок уровня и границы разблокировки из текущего каталога.</summary>
        private static EconomyProgressionState CaptureProgressionState(string levelAddress)
        {
            if (string.IsNullOrWhiteSpace(levelAddress) || !LevelCatalogService.HasCatalog ||
                LevelCatalogService.Catalog.IsEmpty || GameDataManager.PlayerData?.Progress == null ||
                !LevelCatalogService.TryFindLevel(levelAddress, out var current))
                return null;

            var catalog = LevelCatalogService.Catalog;
            var snapshot = GameDataManager.PlayerData.Progress;
            var currentKey = new LevelProgressKey(current.LocationId, current.PartId, current.LevelIndex);
            var ordered = catalog.EnumerateLevels()
                .OrderBy(item => item.LocationIndex)
                .ThenBy(item => item.PartIndex)
                .ThenBy(item => item.LevelIndex)
                .ToList();
            var currentIndex = ordered.FindIndex(item => string.Equals(item.Address, current.Address, StringComparison.OrdinalIgnoreCase));
            var next = currentIndex >= 0 && currentIndex + 1 < ordered.Count
                ? ordered[currentIndex + 1]
                : default(HierarchicalLevelCatalog.LevelDescriptor);
            var hasNext = currentIndex >= 0 && currentIndex + 1 < ordered.Count;
            var state = new EconomyProgressionState
            {
                level_address = current.Address,
                location_id = current.LocationId,
                part_id = current.PartId,
                level_index = current.LevelIndex,
                stars = snapshot.GetStars(currentKey),
                level_unlocked = snapshot.IsLevelUnlocked(currentKey),
                location_stars = catalog.EnumerateLevels()
                    .Where(item => string.Equals(item.LocationId, current.LocationId, StringComparison.OrdinalIgnoreCase))
                    .Sum(item => snapshot.GetStars(new LevelProgressKey(item.LocationId, item.PartId, item.LevelIndex))),
                current_part_stars = catalog.EnumerateLevels(current.LocationIndex, current.PartIndex)
                    .Sum(item => snapshot.GetStars(new LevelProgressKey(item.LocationId, item.PartId, item.LevelIndex))),
                current_part_required_stars = DefaultUnlockPolicy.GetRequiredStarsForNextPart(
                    catalog.EnumerateLevels(current.LocationIndex, current.PartIndex).Count()),
                current_part_completed = catalog.EnumerateLevels(current.LocationIndex, current.PartIndex)
                    .All(item => snapshot.GetStars(new LevelProgressKey(item.LocationId, item.PartId, item.LevelIndex)) > 0),
                stars_to_next_location = new DefaultUnlockPolicy(catalog, DefaultUnlockPolicy.DefaultStarUnlockOffset)
                    .GetRequiredStarsForNextLocation(snapshot, current.LocationId)
            };
            if (catalog.TryGetPart(current.LocationIndex, current.PartId, out var currentPartIndex, out _)
                && catalog.TryGetLocation(current.LocationIndex, out var location)
                && currentPartIndex + 1 < location.PartsOfDay.Count)
            {
                var nextPartFirst = catalog.EnumerateLevels(current.LocationIndex, currentPartIndex + 1).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(nextPartFirst.Address))
                {
                    var nextPartKey = new LevelProgressKey(nextPartFirst.LocationId, nextPartFirst.PartId, nextPartFirst.LevelIndex);
                    state.next_part_first_address = nextPartFirst.Address;
                    state.next_part_first_unlocked = snapshot.IsLevelUnlocked(nextPartKey);
                }
            }
            if (hasNext)
            {
                var nextKey = new LevelProgressKey(next.LocationId, next.PartId, next.LevelIndex);
                state.next_level_address = next.Address;
                state.next_level_stars = snapshot.GetStars(nextKey);
                state.next_level_unlocked = snapshot.IsLevelUnlocked(nextKey);
            }
            return state;
        }

        private static EconomyFlow[] CloneFlows(IEnumerable<EconomyFlow> flows, bool allowEmpty = true)
        {
            var snapshot = flows?.Where(item => item != null).Select(item => new EconomyFlow
            {
                source = item.source,
                resource = item.resource,
                income = item.income,
                expense = item.expense
            }).ToArray() ?? Array.Empty<EconomyFlow>();
            return allowEmpty || snapshot.Length > 0 ? snapshot : null;
        }

        private void Emit(EconomyEvent item)
        {
            item.sequence = ++_journal.State.sequence;
            item.event_id = _session + ":" + item.sequence;
            item.utc = DateTime.UtcNow.ToString("O");
            item.profile_id = _profile;
            // ResetPlayerProgress создаёт новый ProfileId; runtime Generation при рестарте не используется.
            item.save_generation = _profile;
            item.session_id = _session;
            item.revision = GameDataManager.LocalRevision;
            item.build_version = Application.version + ":" + _settings.shortSha + ":" + _settings.buildLabel;
            item.balance_version = _settings.balanceVersion;
            item.cohort = _cohort;
            item.run_id ??= _run;
            item.level ??= _level;
            if (item.active_seconds == 0) item.active_seconds = _active;
            item.lost_packets = _journal.State.lost_packets;
            _journal.Append(JsonUtility.ToJson(item));
            WriteDiagnosticLine(item);
        }

        private void EmitRunTransition(string action, string reason, int value, Action<EconomyRuntimeState> enrich)
        {
            var runtime = CaptureRuntimeState();
            runtime.action = action;
            runtime.reason = reason ?? string.Empty;
            enrich?.Invoke(runtime);
            Emit(new EconomyEvent { type = "run_transition", source = action, detail = reason, value = value,
                confirmed = true, runtime = runtime });
        }

        private EconomyRuntimeState CaptureRuntimeState(string lootDisposition = null)
        {
            var loot = RunLootBuffer.Capture();
            return new EconomyRuntimeState
            {
                loot_disposition = lootDisposition ?? string.Empty,
                run_coins = loot.RunCoins,
                run_crystals = loot.RunCrystals,
                gross_run_coins = loot.GrossCoins,
                wallet_coins = loot.WalletCoins,
                wallet_crystals = loot.WalletCrystals,
                lives_after = CurrentRemainingLives()
            };
        }

        private static EconomyRuntimeState CloneRuntimeState(EconomyRuntimeState runtime, string lootDispositionOverride = null)
        {
            if (runtime == null)
                return string.IsNullOrEmpty(lootDispositionOverride)
                    ? null
                    : new EconomyRuntimeState { loot_disposition = lootDispositionOverride };
            return new EconomyRuntimeState
            {
                action = runtime.action,
                reason = runtime.reason,
                placement = runtime.placement,
                request_id = runtime.request_id,
                loot_disposition = string.IsNullOrEmpty(lootDispositionOverride)
                    ? runtime.loot_disposition
                    : lootDispositionOverride,
                run_coins = runtime.run_coins,
                run_crystals = runtime.run_crystals,
                gross_run_coins = runtime.gross_run_coins,
                wallet_coins = runtime.wallet_coins,
                wallet_crystals = runtime.wallet_crystals,
                spend_total = runtime.spend_total,
                spend_from_run = runtime.spend_from_run,
                spend_from_wallet = runtime.spend_from_wallet,
                lives_after = runtime.lives_after,
                attempt_preserved = runtime.attempt_preserved
            };
        }

        private static void WriteDiagnosticLine(EconomyEvent item)
        {
            if (item == null)
                return;
            switch (item.type)
            {
                case "run_started":
                    DebugManager.DiagEconomy($"[Run] phase=start run={item.run_id} level={item.level} best={item.previous_best_stars} {FormatRuntime(item.runtime)}");
                    break;
                case "run_finished":
                    DebugManager.DiagEconomy($"[Run] phase=finish outcome={item.source} run={item.run_id} level={item.level} stars={item.stars} confirmed={item.confirmed} lives={item.remaining_lives} {FormatRuntime(item.runtime)}");
                    break;
                case "run_transition":
                    DebugManager.DiagEconomy($"[Run] phase={item.source} run={item.run_id} level={item.level} {FormatRuntime(item.runtime)}");
                    break;
            }
        }

        private static string FormatRuntime(EconomyRuntimeState runtime)
        {
            if (runtime == null)
                return string.Empty;
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(runtime.reason)) parts.Add("reason=" + runtime.reason);
            if (!string.IsNullOrEmpty(runtime.placement)) parts.Add("kind=" + runtime.placement);
            if (!string.IsNullOrEmpty(runtime.request_id)) parts.Add("request=" + runtime.request_id);
            if (!string.IsNullOrEmpty(runtime.loot_disposition)) parts.Add("loot=" + runtime.loot_disposition);
            parts.Add($"run_coins={runtime.run_coins}");
            parts.Add($"run_crystals={runtime.run_crystals}");
            parts.Add($"gross_coins={runtime.gross_run_coins}");
            parts.Add($"wallet_coins={runtime.wallet_coins}");
            parts.Add($"wallet_crystals={runtime.wallet_crystals}");
            if (runtime.spend_total > 0)
            {
                parts.Add($"price={runtime.spend_total}");
                parts.Add($"from_run={runtime.spend_from_run}");
                parts.Add($"from_wallet={runtime.spend_from_wallet}");
            }
            if (runtime.lives_after >= 0) parts.Add($"lives_after={runtime.lives_after}");
            if (runtime.attempt_preserved) parts.Add("attempt_preserved=true");
            return string.Join(" ", parts);
        }

        private static bool Ready(bool allowTutorial = false) => Enabled && GameDataManager.IsLoaded &&
            _instance._profile == GameDataManager.ProfileId && !AutomationRuntimePrefs.IsTestLevelAutomationRun() &&
            (allowTutorial || !TutorialStorage.IsPlayerDataBackupActive);

        private static void Safe(Action action)
        {
            try { action(); }
            catch (Exception error) { DebugManager.DiagStability($"[ECO JOURNAL] {error.GetType().Name}: {error.Message}"); }
        }

        private void OnDestroy()
        {
            Application.quitting -= HandleApplicationQuitting;
            GameDataManager.ProfileChanged -= ProfileChanged;
            GameDataManager.ProgressReconciled -= ProgressReconciled;
            GameDataManager.SaveFailed -= SaveFailed;
            GameEventsManager.OnLevelStarted -= StartRun;
            SceneManager.sceneUnloaded -= SceneUnloaded;
            _journal?.Dispose();
            if (_instance == this) _instance = null;
        }
    }
}
