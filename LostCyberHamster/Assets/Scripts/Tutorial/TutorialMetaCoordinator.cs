using System;
using LostCyberHamster.UI;

namespace Assets.Scripts.Tutorial
{
    public static class TutorialMetaCoordinator
    {
        public const int ElectricStrikeSkinId = TutorialUiFlowController.ElectricStrikeSkinId;

        public static TutorialMetaStage Stage => TutorialUiFlowController.Stage;
        public static int RewardCrystals => TutorialUiFlowController.RewardCrystals;
        public static bool IsActive => TutorialUiFlowController.IsActive;
        public static TutorialUiAction CurrentAction => TutorialUiFlowController.CurrentAction;
        public static TutorialUiPrompt CurrentPrompt => TutorialUiFlowController.CurrentPrompt;
        public static ScreenEnum? CurrentCompletionSurface => TutorialUiFlowController.CurrentCompletionSurface;
        public static bool CurrentStepWaitsForSurfaceCompletion =>
            TutorialUiFlowController.CurrentStepWaitsForSurfaceCompletion;
        public static event Action SkinLessonCompleted
        {
            add => TutorialUiFlowController.SkinLessonCompleted += value;
            remove => TutorialUiFlowController.SkinLessonCompleted -= value;
        }

        public static void BeginElectricStrikeSkinLesson()
        {
            TutorialUiFlowController.BeginElectricStrikeSkinLesson();
        }

        public static void ResetForNewTutorialRun()
        {
            TutorialUiRuntime.Reset();
            TutorialUiFlowController.Reset();
        }

        public static TutorialUiFlowResult Notify(
            TutorialUiAction action,
            TutorialUiActionContext context = null)
        {
            return TutorialUiFlowController.Notify(action, context);
        }

        public static TutorialUiFlowResult NotifySurfaceLoaded(ScreenEnum surface)
        {
            return TutorialUiFlowController.NotifySurfaceLoaded(surface);
        }

        public static bool IsTargetSkinPurchased()
        {
            return TutorialUiFlowController.IsTargetSkinPurchased();
        }

        public static bool IsTargetSkinEquipped()
        {
            return TutorialUiFlowController.IsTargetSkinEquipped();
        }

        public static int GetTargetSkinIndex()
        {
            return TutorialUiFlowController.GetTargetSkinIndex();
        }

        public static void RestoreSandbox()
        {
            TutorialUiFlowController.RestoreSandbox();
        }

        public static void RestoreSandbox(bool markTutorialCompleted)
        {
            TutorialUiFlowController.RestoreSandbox(markTutorialCompleted);
        }
    }
}
