#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
using GameManagement;
using GameManagement.Progress;
using LostCyberHamster.UI;
using UnityEngine;
using Vues.GameCore;

namespace Assets.Scripts.DevTools.SkateboardTesting
{
    /// <summary>
    /// Управляет подготовкой Skateboard и state-driven gameplay-сценариями Tools/Testing.
    /// </summary>
    public sealed class SkateboardTestingRunner
    {
        private const int _skateboardId = 3;
        private const float _betweenGroupsDelay = 0.5f;

        private readonly PlayerExperienceService _experienceService = new();
        private readonly List<int> _observedImpactDepths = new(3);

        private Hamster _hamster;
        private SkateboardAttack _attack;
        private GameManager _gameManager;
        private int[] _comboGroups = Array.Empty<int>();
        private int[] _expectedImpactDepths = Array.Empty<int>();
        private int _groupIndex;
        private int _cycleInGroup;
        private int _cyclesScheduled;
        private float _nextGroupAt;
        private bool _waitingForQueuedCycle;
        private bool _waitingForRide;
        private bool _waitingForCompletion;
        private bool _landingHandled;
        private bool _isTimeoutScenario;
        private bool _useSuperJump;
        private bool _isBusy;
        private string _scenarioTitle = string.Empty;
        private string _status = "Запустите Bootstrap. Подготовку выполняйте в Menu.";

        private SkateboardTestingRunner()
        {
        }

        public static SkateboardTestingRunner Shared { get; } = new();

        public event Action Changed;

        public bool IsBusy => _isBusy;
        public bool UseSuperJump => _useSuperJump;
        public string Status => _status;
        public string LiveStatus => BuildLiveStatus();

        public bool CanPrepare =>
            Application.isPlaying &&
            !_isBusy &&
            GameDataManager.PlayerData != null &&
            SuperAttackService.TryGet(_skateboardId, out _);

        // Во время активного scenario кнопки остаются доступны: повторное нажатие
        // проверяет rejected activation без сброса текущего mode.
        public bool CanEnterMode =>
            TryResolveGameplay(out _, out _, out GameManager manager) &&
            manager.State == GameState.PLAYING;
        public bool CanRunScenario => CanEnterMode;
        public bool CanPause => TryGetGameManager(out GameManager manager) &&
                                manager.State == GameState.PLAYING;
        public bool CanResume => TryGetGameManager(out GameManager manager) &&
                                 manager.State == GameState.PAUSED;
        public bool CanCancel =>
            _isBusy ||
            (TryResolveGameplay(out _, out SkateboardAttack attack, out _) &&
             attack.IsActive);

        /// <summary>
        /// Выдаёт ровно недостающий XP, проверяет unlock и выбирает Skateboard.
        /// </summary>
        public void PrepareUnlockAndSelectSkateboard()
        {
            if (!CanPrepare)
            {
                SetStatus("Подготовка недоступна: дождитесь загрузки PlayerData и каталога.");
                return;
            }

            try
            {
                PlayerData playerData = GameDataManager.PlayerData;
                if (!SuperAttackService.TryGet(
                        _skateboardId,
                        out SuperAttackData skateboard))
                {
                    throw new InvalidOperationException(
                        "Skateboard ID 3 отсутствует в каталоге.");
                }

                int beforeLevel = playerData.PlayerLevel;
                int beforeExperience = playerData.ExperiencePoints;
                int missingExperience = CalculateMissingExperience(
                    playerData,
                    skateboard.RequiredPlayerLevel);
                if (missingExperience > 0)
                {
                    _experienceService.GrantExperienceForTesting(
                        playerData,
                        missingExperience);
                }

                if (!SuperAttackService.IsUnlocked(
                        _skateboardId,
                        playerData.PlayerLevel))
                {
                    throw new InvalidOperationException(
                        "Skateboard не открылся после начисления XP.");
                }

                if (!SuperAttackService.TrySelect(_skateboardId) ||
                    SuperAttackService.ActiveSuperAttackId != _skateboardId)
                {
                    throw new InvalidOperationException(
                        "SuperAttackService не выбрал Skateboard.");
                }

                UIManager.OnRepaintScreen?.Invoke();
                string runtimeNote = TryFindHamster(out _)
                    ? " Текущий Hamster не заменяется: войдите в следующий уровень."
                    : string.Empty;
                SetStatus(
                    $"PASS: Skateboard открыт и выбран. " +
                    $"Level {beforeLevel}, XP {beforeExperience} -> " +
                    $"Level {playerData.PlayerLevel}, XP {playerData.ExperiencePoints}; " +
                    $"добавлено {missingExperience} XP.{runtimeNote}");
            }
            catch (Exception exception)
            {
                SetStatus($"FAIL: {exception.Message}", isError: true);
            }
        }

        /// <summary>
        /// Заполняет charge и входит в mode через настоящий UltaEvent.
        /// </summary>
        public void EnterMode()
        {
            if (!TryResolveGameplay(
                    out Hamster hamster,
                    out SkateboardAttack attack,
                    out _))
            {
                SetStatus(GetGameplayUnavailableStatus());
                return;
            }

            if (attack.IsActive)
            {
                ProbeRejectedActivation(hamster, attack);
                return;
            }

            if (!TryEnterMode(hamster, attack, out string error))
            {
                SetStatus($"FAIL: {error}", isError: true);
                return;
            }

            SetStatus("PASS: Skateboard mode активирован через charge + UltaEvent.");
        }

        public void RunTimeoutScenario()
        {
            StartScenario("Timeout 10 s", Array.Empty<int>(), Array.Empty<int>());
        }

        public void RunOnePlusOnePlusOneScenario()
        {
            StartScenario("1 + 1 + 1", new[] { 1, 1, 1 }, new[] { 1, 1, 1 });
        }

        public void RunTwoPlusOneScenario()
        {
            StartScenario("2 + 1", new[] { 2, 1 }, new[] { 1, 2, 1 });
        }

        public void RunOnePlusTwoScenario()
        {
            StartScenario("1 + 2", new[] { 1, 2 }, new[] { 1, 1, 2 });
        }

        public void RunThreeComboScenario()
        {
            StartScenario("3 Combo", new[] { 3 }, new[] { 1, 2, 3 });
        }

        public void SetUseSuperJump(bool useSuperJump)
        {
            if (_isBusy)
                return;

            _useSuperJump = useSuperJump;
            SetStatus(useSuperJump
                ? "Super Jump включён для каждого cycle."
                : "Обычный Jump включён для каждого cycle.");
        }

        public void Pause()
        {
            if (!CanPause || !TryGetGameManager(out GameManager manager))
                return;

            manager.Pause();
            SetStatus(_isBusy
                ? $"PAUSED: {_scenarioTitle}. Scenario продолжится после Resume."
                : "Game paused.");
        }

        public void Resume()
        {
            if (!CanResume || !TryGetGameManager(out GameManager manager))
                return;

            manager.Resume();
            SetStatus(_isBusy
                ? $"RUNNING: {_scenarioTitle}."
                : "Game resumed.");
        }

        public void Cancel()
        {
            SkateboardAttack attack = _attack;
            StopScenario(clearStatus: false);
            if (attack == null &&
                TryResolveGameplay(out _, out SkateboardAttack resolved, out _))
            {
                attack = resolved;
            }

            if (attack?.IsActive == true)
                attack.Complete();

            SetStatus("Scenario отменён; Skateboard mode завершён.");
        }

        /// <summary>
        /// Продвигает runner только по реально наблюдаемым состояниям Skateboard FSM.
        /// </summary>
        public void Tick()
        {
            if (!_isBusy)
                return;

            if (_gameManager == null || _attack == null || _hamster == null)
            {
                FailScenario("Gameplay runtime потерян.");
                return;
            }

            if (_gameManager.State == GameState.PAUSED)
                return;
            if (_gameManager.State != GameState.PLAYING)
            {
                FailScenario($"GameState стал {_gameManager.State}.");
                return;
            }

            if (_isTimeoutScenario)
            {
                if (!_attack.IsActive)
                    PassScenario("Mode завершился после timeout и вернул normal actor.");
                return;
            }

            TickComboScenario();
        }

        public void HandlePlayModeStarted()
        {
            StopScenario(clearStatus: true);
            SetStatus("Play Mode готов. В Menu подготовьте и выберите Skateboard.");
        }

        public void HandlePlayModeStopped()
        {
            StopScenario(clearStatus: true);
            SetStatus("Play Mode остановлен.");
        }

        private void StartScenario(
            string title,
            int[] comboGroups,
            int[] expectedImpactDepths)
        {
            if (!TryResolveGameplay(
                    out Hamster hamster,
                    out SkateboardAttack attack,
                    out GameManager gameManager))
            {
                SetStatus(GetGameplayUnavailableStatus());
                return;
            }

            // Повторная scenario-кнопка проверяет active rejection и не заменяет текущий script.
            if (attack.IsActive)
            {
                ProbeRejectedActivation(hamster, attack);
                return;
            }

            if (_isBusy)
            {
                SetStatus("Scenario уже выполняется, но active mode не найден.", isError: true);
                return;
            }

            if (!TryEnterMode(hamster, attack, out string error))
            {
                SetStatus($"FAIL: {error}", isError: true);
                return;
            }

            _hamster = hamster;
            _attack = attack;
            _gameManager = gameManager;
            _comboGroups = comboGroups;
            _expectedImpactDepths = expectedImpactDepths;
            _observedImpactDepths.Clear();
            _groupIndex = 0;
            _cycleInGroup = 0;
            _cyclesScheduled = 0;
            _nextGroupAt = Time.time;
            _waitingForQueuedCycle = false;
            _waitingForRide = false;
            _waitingForCompletion = false;
            _landingHandled = false;
            _isTimeoutScenario = comboGroups.Length == 0;
            _scenarioTitle = title;
            _isBusy = true;
            _attack.LandingImpact += OnLandingImpact;
            SetStatus($"RUNNING: {title}; jump={(_useSuperJump ? "Super" : "Normal")}.");
        }

        private void TickComboScenario()
        {
            if (!_attack.IsActive)
            {
                if (_waitingForCompletion &&
                    _cyclesScheduled == SkateboardAttack.DefaultJumpBudget)
                {
                    ValidateAndPassComboScenario();
                }
                else
                {
                    FailScenario(
                        $"Mode завершился раньше: cycles={_cyclesScheduled}/" +
                        $"{SkateboardAttack.DefaultJumpBudget}.");
                }

                return;
            }

            if (_waitingForCompletion)
                return;

            if (_waitingForQueuedCycle)
            {
                if (_attack.IsJumping)
                {
                    _waitingForQueuedCycle = false;
                    _landingHandled = false;
                    SetStatus(
                        $"RUNNING: {_scenarioTitle}; cycle {_cyclesScheduled}/3 started, " +
                        $"combo {_attack.ComboDepth}.");
                }

                return;
            }

            if (_waitingForRide)
            {
                if (!_attack.IsRiding)
                    return;

                _waitingForRide = false;
                _groupIndex++;
                _cycleInGroup = 0;
                _landingHandled = false;
                _nextGroupAt = Time.time + _betweenGroupsDelay;
                return;
            }

            if (_cycleInGroup == 0)
            {
                if (!_attack.IsRiding || Time.time < _nextGroupAt)
                    return;

                if (!RequestJumpCycle())
                {
                    FailScenario("Первый jump группы отклонён runtime.");
                    return;
                }

                _cycleInGroup = 1;
                _cyclesScheduled++;
                _landingHandled = false;
                SetStatus(
                    $"RUNNING: {_scenarioTitle}; cycle {_cyclesScheduled}/3 started.");
                return;
            }

            if (!_attack.IsLanding || _landingHandled)
                return;

            _landingHandled = true;
            int groupSize = _comboGroups[_groupIndex];
            if (_cycleInGroup < groupSize)
            {
                if (!RequestJumpCycle())
                {
                    FailScenario("Queued jump группы отклонён runtime.");
                    return;
                }

                _cycleInGroup++;
                _cyclesScheduled++;
                _waitingForQueuedCycle = true;
                SetStatus(
                    $"RUNNING: {_scenarioTitle}; queued cycle {_cyclesScheduled}/3.");
                return;
            }

            bool isLastGroup = _groupIndex == _comboGroups.Length - 1;
            if (isLastGroup)
            {
                _waitingForCompletion = true;
                SetStatus($"RUNNING: {_scenarioTitle}; ждём final landing tail.");
            }
            else
            {
                _waitingForRide = true;
                SetStatus($"RUNNING: {_scenarioTitle}; ждём Ride перед следующей группой.");
            }
        }

        private bool RequestJumpCycle()
        {
            int beforeBudget = _attack.JumpsRemaining;
            _hamster.JumpRequest.Invoke();

            // В Landing budget уменьшится только при фактическом старте queued cycle.
            bool accepted = _attack.IsJumping ||
                            (_attack.IsLanding &&
                             _attack.JumpsRemaining == beforeBudget);
            if (!accepted)
                return false;

            if (_useSuperJump)
                _hamster.SuperJumpRequest.Invoke();
            return true;
        }

        private void ProbeRejectedActivation(
            Hamster hamster,
            SkateboardAttack attack)
        {
            int previousCharge = hamster.UltaChargeAmount.Value;
            int jumpsBefore = attack.JumpsRemaining;
            int comboBefore = attack.ComboDepth;
            bool wasRiding = attack.IsRiding;
            bool wasJumping = attack.IsJumping;
            bool wasLanding = attack.IsLanding;

            hamster.UltaChargeAmount.Value = 100;
            hamster.UltaEvent.Invoke();
            hamster.UltaChargeAmount.Value = previousCharge;

            bool unchanged = attack.IsActive &&
                             attack.JumpsRemaining == jumpsBefore &&
                             attack.ComboDepth == comboBefore &&
                             attack.IsRiding == wasRiding &&
                             attack.IsJumping == wasJumping &&
                             attack.IsLanding == wasLanding;
            SetStatus(unchanged
                ? "PASS: repeated activation отклонена; текущий mode не сброшен."
                : "FAIL: repeated activation изменила active Skateboard state.",
                isError: !unchanged);
        }

        private static bool TryEnterMode(
            Hamster hamster,
            SkateboardAttack attack,
            out string error)
        {
            if (attack.IsActive)
            {
                error = "Skateboard уже активен.";
                return false;
            }

            hamster.UltaChargeAmount.Value = 100;
            hamster.UltaEvent.Invoke();
            if (!attack.IsActive)
            {
                error =
                    "UltaEvent не активировал Skateboard. Требуется PLAYING + stable Run/RoofRun.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void OnLandingImpact(int comboDepth, bool isSuperCycle)
        {
            if (!_isBusy || _isTimeoutScenario)
                return;

            _observedImpactDepths.Add(comboDepth);
            SetStatus(
                $"RUNNING: {_scenarioTitle}; impact combo={comboDepth}, " +
                $"cycle={(isSuperCycle ? "Super" : "Normal")}.");
        }

        private void ValidateAndPassComboScenario()
        {
            if (_observedImpactDepths.Count != _expectedImpactDepths.Length)
            {
                FailScenario(
                    $"Impact count {_observedImpactDepths.Count}, " +
                    $"expected {_expectedImpactDepths.Length}.");
                return;
            }

            for (int index = 0; index < _expectedImpactDepths.Length; index++)
            {
                if (_observedImpactDepths[index] == _expectedImpactDepths[index])
                    continue;

                FailScenario(
                    $"Impact #{index + 1}: {_observedImpactDepths[index]}, " +
                    $"expected {_expectedImpactDepths[index]}.");
                return;
            }

            PassScenario(
                $"Impacts [{string.Join(",", _observedImpactDepths)}], " +
                "budget exhausted, normal actor restored.");
        }

        private void PassScenario(string details)
        {
            string title = _scenarioTitle;
            StopScenario(clearStatus: false);
            SetStatus($"PASS: {title}. {details}");
        }

        private void FailScenario(string details)
        {
            string title = _scenarioTitle;
            StopScenario(clearStatus: false);
            SetStatus($"FAIL: {title}. {details}", isError: true);
        }

        private void StopScenario(bool clearStatus)
        {
            if (_attack != null)
                _attack.LandingImpact -= OnLandingImpact;

            _hamster = null;
            _attack = null;
            _gameManager = null;
            _comboGroups = Array.Empty<int>();
            _expectedImpactDepths = Array.Empty<int>();
            _observedImpactDepths.Clear();
            _groupIndex = 0;
            _cycleInGroup = 0;
            _cyclesScheduled = 0;
            _waitingForQueuedCycle = false;
            _waitingForRide = false;
            _waitingForCompletion = false;
            _landingHandled = false;
            _isTimeoutScenario = false;
            _scenarioTitle = string.Empty;
            _isBusy = false;
            if (clearStatus)
                _status = string.Empty;
        }

        private static int CalculateMissingExperience(
            PlayerData playerData,
            int requiredLevel)
        {
            if (playerData.PlayerLevel >= requiredLevel)
                return 0;

            int levelsMissing = checked(requiredLevel - playerData.PlayerLevel);
            return checked(
                levelsMissing * PlayerExperienceService.PlayerLevelThreshold -
                playerData.ExperiencePoints);
        }

        private string BuildLiveStatus()
        {
            PlayerData playerData = GameDataManager.PlayerData;
            string player = playerData == null
                ? "PlayerData: unavailable"
                : $"Player: level={playerData.PlayerLevel}, xp={playerData.ExperiencePoints}, " +
                  $"selected={SuperAttackService.ActiveSuperAttackId?.ToString() ?? "none"}";

            if (!TryResolveGameplay(
                    out Hamster hamster,
                    out SkateboardAttack attack,
                    out GameManager gameManager))
            {
                return player + "\nGameplay: unavailable; enter a level after selecting Skateboard.";
            }

            string phase = !attack.IsActive
                ? "Inactive"
                : attack.IsRiding
                    ? "Ride"
                    : attack.IsLanding
                        ? "Landing"
                        : attack.IsSuperJumping
                            ? "SuperJump"
                            : attack.IsJumping
                                ? "Jump"
                                : "Unknown";
            return player + "\n" +
                   $"Game={gameManager.State}, Hamster={hamster.HamsterState.Value}, " +
                   $"lane={(hamster.IsOnBottomLine.Value ? "Bottom" : "Top")}\n" +
                   $"Mode={phase}, actor=" +
                   $"{(hamster.ActorSwitcher.IsSkateboardActive ? "Skateboard" : "Normal")}, " +
                   $"surface={hamster.SkateboardSurfaceController.State}\n" +
                   $"jumps={attack.JumpsRemaining}, combo={attack.ComboDepth}, " +
                   $"charge={hamster.UltaChargeAmount.Value}, lives={hamster.Lives.Value}";
        }

        private static bool TryResolveGameplay(
            out Hamster hamster,
            out SkateboardAttack attack,
            out GameManager gameManager)
        {
            hamster = null;
            attack = null;
            gameManager = null;
            if (!Application.isPlaying || !TryFindHamster(out hamster))
                return false;

            attack = hamster.SkateboardAttackRuntimeForTesting;
            gameManager = LevelController.Instance?.LevelData?.GameManager;
            return attack != null && gameManager != null;
        }

        private static bool TryFindHamster(out Hamster hamster)
        {
            hamster = LevelController.Instance?.LevelData?.Hamster;
            if (hamster != null)
                return true;

            hamster = UnityEngine.Object.FindAnyObjectByType<Hamster>(
                FindObjectsInactive.Include);
            return hamster != null;
        }

        private static bool TryGetGameManager(out GameManager gameManager)
        {
            gameManager = LevelController.Instance?.LevelData?.GameManager;
            return Application.isPlaying && gameManager != null;
        }

        private static string GetGameplayUnavailableStatus()
        {
            return "Gameplay Skateboard runtime недоступен. " +
                   "Выберите Skateboard в Menu и войдите в новый уровень.";
        }

        private void SetStatus(string status, bool isError = false)
        {
            _status = status;
            if (isError)
                Debug.LogError($"[Skateboard Testing] {status}");
            else
                Debug.Log($"[Skateboard Testing] {status}");
            Changed?.Invoke();
        }
    }
}
#endif
