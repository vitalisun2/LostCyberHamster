using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Координирует план и preview-рендер: применяет новую ветку, очищает состояние, логирует выбор.
    /// Не знает про executor — управление шагами на стороне вызывающего.
    /// </summary>
    internal class BotPlanRuntime
    {
        private readonly CurrentPlan _plan;
        private readonly BotBranchRenderer _branchRenderer;
        private readonly List<AvoidanceCommitment> _activeAvoidanceCommitments = new List<AvoidanceCommitment>();

        private int _lastPlanTargetId;

        public bool HasCommittedPlan => !_plan.IsEmpty;
        public bool HasPreview => _branchRenderer.HasPreview;

        public BotPlanRuntime(CurrentPlan plan, BotBranchRenderer branchRenderer)
        {
            _plan = plan;
            _branchRenderer = branchRenderer;
        }

        /// <summary>
        /// Коммитит новую ветку как текущий committed plan.
        /// </summary>
        public BranchStep CommitPlan(BotSceneSnapshot snapshot, BranchCandidate best, bool hamsterOnBottomFallback)
        {
            ApplyPlanningMemory(snapshot);

            if (best == null || best.Steps.Count == 0)
            {
                Clear();
                return null;
            }

            LogPlanSelectedIfChanged(best);
            _plan.ReplaceFrom(best, best.Steps[0].Reason);
            UpdatePreview(snapshot, hamsterOnBottomFallback);
            return _plan.Head;
        }

        /// <summary>
        /// Продвигает committed plan после завершения head-шага.
        /// </summary>
        public BranchStep AdvancePlan(BotSceneSnapshot snapshot, bool hamsterOnBottomFallback)
        {
            RegisterCommitmentsFromCompletedHead(snapshot);
            _plan.AdvanceCompletedHead();
            ApplyPlanningMemory(snapshot);
            if (_plan.IsEmpty)
            {
                LogPlanClearedIfNeeded();
                _branchRenderer.ClearPreview();
                return null;
            }

            _lastPlanTargetId = _plan.Head.TargetObstacle.StableId;
            UpdatePreview(snapshot, hamsterOnBottomFallback);
            return _plan.Head;
        }

        public List<BranchStep> SnapshotRetainableSteps()
        {
            return _plan.SnapshotRetainableSteps();
        }

        public void Clear()
        {
            LogPlanClearedIfNeeded();
            _plan.Clear();
            _branchRenderer.ClearPreview();
        }

        public void ApplyPlanningMemory(BotSceneSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            PruneInactiveCommitments(snapshot);
            snapshot.ReplaceAvoidanceCommitments(_activeAvoidanceCommitments);
        }

        public void ResetSelectionTracking()
        {
            _lastPlanTargetId = 0;
        }

        public void ResetPlanningMemory()
        {
            _activeAvoidanceCommitments.Clear();
        }

        public void Render(Camera camera)
        {
            _branchRenderer.Render(camera);
        }

        public void Dispose()
        {
            _branchRenderer.Dispose();
        }

        private void UpdatePreview(BotSceneSnapshot snapshot, bool hamsterOnBottomFallback)
        {
            _branchRenderer.UpdatePreview(
                _plan.Steps,
                snapshot != null ? snapshot.HamsterOnBottom : hamsterOnBottomFallback);
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

        private void RegisterCommitmentsFromCompletedHead(BotSceneSnapshot snapshot)
        {
            var completedHead = _plan.Head;
            if (completedHead == null || completedHead.Status != BranchStepStatus.Completed)
            {
                PruneInactiveCommitments(snapshot);
                return;
            }

            if (completedHead.Action == BotAction.SwitchLane)
            {
                AddOrReplaceCommitment(new AvoidanceCommitment(
                    completedHead.TargetObstacle.StableId,
                    forbiddenLaneBottom: !completedHead.TargetObstacle.IsTopLane));
            }

            PruneInactiveCommitments(snapshot);
        }

        private void AddOrReplaceCommitment(AvoidanceCommitment commitment)
        {
            for (int i = _activeAvoidanceCommitments.Count - 1; i >= 0; i--)
            {
                if (_activeAvoidanceCommitments[i].ThreatStableId == commitment.ThreatStableId)
                    _activeAvoidanceCommitments.RemoveAt(i);
            }

            _activeAvoidanceCommitments.Add(commitment);
        }

        private void PruneInactiveCommitments(BotSceneSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            snapshot.ReplaceAvoidanceCommitments(_activeAvoidanceCommitments);
            snapshot.PruneInactiveAvoidanceCommitments();

            _activeAvoidanceCommitments.Clear();
            _activeAvoidanceCommitments.AddRange(snapshot.ActiveAvoidanceCommitments);
        }
    }
}
