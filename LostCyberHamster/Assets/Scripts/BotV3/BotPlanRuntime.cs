using UnityEngine;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Координирует план и preview-рендер: применяет новую ветку, очищает состояние, логирует выбор.
    /// Не знает про executor — управление шагами на стороне вызывающего.
    /// </summary>
    internal class BotPlanRuntime
    {
        private readonly CurrentPlan _plan;
        private readonly BotBranchRenderer _branchRenderer;

        private int _lastPlanTargetId;

        public bool HasPreview => _branchRenderer.HasPreview;

        public BotPlanRuntime(CurrentPlan plan, BotBranchRenderer branchRenderer)
        {
            _plan = plan;
            _branchRenderer = branchRenderer;
        }

        /// <summary>
        /// Применяет новый план. Возвращает head-шаг для передачи executor'у, или null если план пуст.
        /// </summary>
        public BranchStep ApplyPlan(BotSceneSnapshot snapshot, BranchCandidate best, bool hamsterOnBottomFallback)
        {
            _plan.RemoveCompletedFromHead();

            if (best == null || best.Steps.Count == 0)
            {
                LogPlanClearedIfNeeded();
                _plan.Clear();
                _branchRenderer.ClearPreview();
                return null;
            }

            LogPlanSelectedIfChanged(best);
            _plan.ReplaceFrom(best, best.Steps[0].Reason);
            _branchRenderer.UpdatePreview(
                _plan.Steps,
                snapshot != null ? snapshot.HamsterOnBottom : hamsterOnBottomFallback);

            return _plan.Head;
        }

        public void Clear()
        {
            _plan.Clear();
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
