using System;
using UnityEngine;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Координирует runtime-план: выбор/очистку текущей ветви, executor и preview-render state.
    /// </summary>
    internal class BotPlanRuntime
    {
        private readonly CurrentPlan _plan;
        private readonly StepExecutor _executor;
        private readonly BotBranchRenderer _branchRenderer;

        private int _lastPlanTargetId;

        public bool IsStepInProgress => _executor.IsStepInProgress;
        public bool HasPreview => _branchRenderer.HasPreview;

        public event Action OnStepCompleted
        {
            add => _executor.OnStepCompleted += value;
            remove => _executor.OnStepCompleted -= value;
        }

        public BotPlanRuntime(CurrentPlan plan, StepExecutor executor, BotBranchRenderer branchRenderer)
        {
            _plan = plan;
            _executor = executor;
            _branchRenderer = branchRenderer;
        }

        public void PollCompletion() => _executor.PollCompletion();

        public void TryFire() => _executor.TryFire();

        public void RemoveCompletedFromHead()
        {
            _plan.RemoveCompletedFromHead();
        }

        public void ApplyPlan(BotSceneSnapshot snapshot, BranchCandidate best, bool hamsterOnBottomFallback)
        {
            if (best == null || best.Steps.Count == 0)
            {
                LogPlanClearedIfNeeded();
                ClearRuntime();
                return;
            }

            LogPlanSelectedIfChanged(best);
            _plan.ReplaceFrom(best, best.Steps[0].Reason);
            _branchRenderer.UpdatePreview(
                _plan.Steps,
                snapshot != null ? snapshot.HamsterOnBottom : hamsterOnBottomFallback);

            if (_plan.Head != null)
                _executor.SetStep(_plan.Head);
        }

        public void ClearRuntime()
        {
            _plan.Clear();
            _executor.ClearStep();
            _branchRenderer.ClearPreview();
        }

        public void ResetSelectionTracking()
        {
            _lastPlanTargetId = 0;
        }

        public void Render(Camera camera)
        {
            _branchRenderer.Render(camera);
        }

        public void Dispose()
        {
            _branchRenderer.Dispose();
        }

        private void LogPlanSelectedIfChanged(BranchCandidate best)
        {
            int newTargetId = best.Steps[0].TargetObstacle.StableId;
            if (newTargetId == _lastPlanTargetId)
                return;

            _lastPlanTargetId = newTargetId;
            BotLogger.LogPlanSelected(best);
        }

        private void LogPlanClearedIfNeeded()
        {
            if (_lastPlanTargetId == 0)
                return;

            BotLogger.LogPlanCleared();
            _lastPlanTargetId = 0;
        }
    }
}
