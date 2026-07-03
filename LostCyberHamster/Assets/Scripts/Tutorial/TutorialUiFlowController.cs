using System;
using System.Collections.Generic;
using System.Linq;
using GameManagement;
using LostCyberHamster.UI;

namespace Assets.Scripts.Tutorial
{
    public static class TutorialUiFlowController
    {
        public const int ElectricStrikeSkinId = 2;

        private const int _fallbackElectricStrikePrice = 20;

        private static readonly IReadOnlyList<TutorialUiStep> _electricStrikeSkinSteps = new[]
        {
            new TutorialUiStep(
                TutorialMetaStage.AwaitingHomeCharacter,
                TutorialUiAction.OpenCharacterScreen,
                new TutorialUiPrompt(
                    ScreenEnum.HomeScreen,
                    TutorialUiTarget.HomeCharacterButton,
                    "Откройте раздел скинов",
                    TutorialFocusShape.Circle),
                ScreenEnum.CharacterScreen),
            new TutorialUiStep(
                TutorialMetaStage.AwaitingSkinSelection,
                TutorialUiAction.SelectNextSkin,
                new TutorialUiPrompt(
                    ScreenEnum.CharacterScreen,
                    TutorialUiTarget.SkinNextButton,
                    "Листайте до скина с молнией",
                    TutorialFocusShape.Circle)),
            new TutorialUiStep(
                TutorialMetaStage.AwaitingSkinPurchase,
                TutorialUiAction.BuySkin,
                new TutorialUiPrompt(
                    ScreenEnum.CharacterScreen,
                    TutorialUiTarget.SkinChangeButton,
                    "Купите скин с молнией",
                    TutorialFocusShape.RoundedRect)),
            new TutorialUiStep(
                TutorialMetaStage.AwaitingSkinEquip,
                TutorialUiAction.EquipSkin,
                new TutorialUiPrompt(
                    ScreenEnum.CharacterScreen,
                    TutorialUiTarget.SkinChangeButton,
                    "Наденьте скин",
                    TutorialFocusShape.RoundedRect))
        };

        private static IReadOnlyList<TutorialUiStep> _steps = Array.Empty<TutorialUiStep>();
        private static int _currentStepIndex = -1;

        public static TutorialMetaStage Stage { get; private set; } = TutorialMetaStage.None;
        public static int RewardCrystals { get; private set; } = _fallbackElectricStrikePrice;
        public static bool IsActive => Stage != TutorialMetaStage.None && Stage != TutorialMetaStage.Completed;
        public static TutorialUiAction CurrentAction => CurrentStep?.Action ?? default;
        public static TutorialUiPrompt CurrentPrompt => IsActive ? CurrentStep?.Prompt : null;
        public static ScreenEnum? CurrentCompletionSurface => IsActive ? CurrentStep?.CompletionSurface : null;
        public static bool CurrentStepWaitsForSurfaceCompletion => CurrentStep?.WaitsForSurfaceCompletion == true;
        public static event Action SkinLessonCompleted;

        private static TutorialUiStep CurrentStep =>
            _currentStepIndex >= 0 && _currentStepIndex < _steps.Count
                ? _steps[_currentStepIndex]
                : null;

        public static void BeginElectricStrikeSkinLesson()
        {
            RewardCrystals = GetTargetSkinPrice();
            TutorialSandboxState.PrepareSkinPurchaseLesson(RewardCrystals);
            _steps = _electricStrikeSkinSteps;
            _currentStepIndex = 0;
            Stage = CurrentStep.Stage;
            TutorialUiRuntime.Activate();
            Log(
                $"skin lesson started targetSkin={ElectricStrikeSkinId} trainingCrystals={RewardCrystals} " +
                $"skinOrder={GetSkinOrderForLog()} currentSkin={SkinManager.CurrentSkin?.Id} " +
                $"targetIndex={GetTargetSkinIndex()}");
        }

        public static void Reset()
        {
            RewardCrystals = _fallbackElectricStrikePrice;
            _steps = Array.Empty<TutorialUiStep>();
            _currentStepIndex = -1;
            Stage = TutorialMetaStage.None;
            Log("reset");
        }

        public static TutorialUiFlowResult Notify(
            TutorialUiAction action,
            TutorialUiActionContext context = null)
        {
            if (!IsActive || CurrentStep?.Action != action)
            {
                return TutorialUiFlowResult.Ignored();
            }

            context ??= TutorialUiActionContext.Empty;
            if (!IsCurrentStepCompleted(context))
            {
                Log(
                    $"stayed stage={Stage} action={action} " +
                    $"contextSkin={(context.HasSkin ? context.SkinId.ToString() : "<none>")} " +
                    $"currentSkin={SkinManager.CurrentSkin?.Id} targetSkin={ElectricStrikeSkinId}");
                return TutorialUiFlowResult.Stayed();
            }

            return Advance();
        }

        public static TutorialUiFlowResult NotifySurfaceLoaded(ScreenEnum surface)
        {
            var currentStep = CurrentStep;
            if (!IsActive || currentStep == null || !currentStep.IsCompletedBySurface(surface))
            {
                return TutorialUiFlowResult.Ignored();
            }

            Log($"surface completed stage={Stage} action={currentStep.Action} surface={surface}");
            return Advance();
        }

        public static bool IsTargetSkinPurchased()
        {
            return GameDataManager.PlayerData.PurchasedSkinIds.Contains(ElectricStrikeSkinId);
        }

        public static bool IsTargetSkinEquipped()
        {
            return SkinManager.CurrentSkin?.Id == ElectricStrikeSkinId;
        }

        public static int GetTargetSkinIndex()
        {
            int index = SkinManager.AvailableSkins.FindIndex(skin => skin.Id == ElectricStrikeSkinId);
            return index >= 0 ? index : 0;
        }

        public static void RestoreSandbox()
        {
            TutorialSandboxState.RestoreRealState();
        }

        public static void RestoreSandbox(bool markTutorialCompleted)
        {
            TutorialSandboxState.RestoreRealState(markTutorialCompleted);
        }

        private static bool IsCurrentStepCompleted(TutorialUiActionContext context)
        {
            switch (Stage)
            {
                case TutorialMetaStage.AwaitingSkinSelection:
                    return context.HasSkin && context.SkinId == ElectricStrikeSkinId;
                case TutorialMetaStage.AwaitingSkinPurchase:
                    return context.HasSkin
                           && context.SkinId == ElectricStrikeSkinId
                           && IsTargetSkinPurchased();
                case TutorialMetaStage.AwaitingSkinEquip:
                    return context.HasSkin
                           && context.SkinId == ElectricStrikeSkinId
                           && IsTargetSkinEquipped();
                default:
                    return true;
            }
        }

        private static TutorialUiFlowResult Advance()
        {
            if (_currentStepIndex + 1 >= _steps.Count)
            {
                Complete();
                return TutorialUiFlowResult.Completed();
            }

            _currentStepIndex++;
            Stage = CurrentStep.Stage;
            Log($"advanced stage={Stage}");
            return TutorialUiFlowResult.Advanced();
        }

        private static void Complete()
        {
            Stage = TutorialMetaStage.Completed;
            _currentStepIndex = -1;
            _steps = Array.Empty<TutorialUiStep>();
            Log($"skin lesson completed appliedSkin={SkinManager.CurrentSkin?.Id}");
            SkinLessonCompleted?.Invoke();
        }

        private static int GetTargetSkinPrice()
        {
            return SkinManager.AvailableSkins
                .FirstOrDefault(skin => skin.Id == ElectricStrikeSkinId)
                ?.Price ?? _fallbackElectricStrikePrice;
        }

        private static string GetSkinOrderForLog()
        {
            return string.Join(",", SkinManager.AvailableSkins.Select(skin => skin.Id));
        }

        private static void Log(string message)
        {
            DebugManager.DiagStability($"[TUTORIAL UI] {message}");
        }
    }
}
