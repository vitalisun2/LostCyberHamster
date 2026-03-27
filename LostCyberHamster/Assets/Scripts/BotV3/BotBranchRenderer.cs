using System.Collections.Generic;
using Assets.Scripts.System;
using UnityEngine;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Рендерит в Game view всю выбранную ветку BotV3.
    /// Координаты подтягиваются по live obstacle instance, чтобы стрелки ехали вместе с миром.
    /// </summary>
    internal sealed class BotBranchRenderer
    {
        private const float LaneYOffset = 0.95f;
        private const float MinTailAlpha = 0.25f;

        private readonly List<PreviewStep> _previewSteps = new List<PreviewStep>();

        private Material _glMaterial;
        private bool _initialHamsterOnBottom;
        private float _previewStartTime;

        public bool HasPreview => _previewSteps.Count > 0;

        public void UpdatePreview(IReadOnlyList<BranchStep> steps, bool hamsterOnBottom)
        {
            if (steps == null || steps.Count == 0)
            {
                ClearPreview();
                return;
            }

            int firstChangedIndex = FindFirstChangedIndex(steps);
            if (firstChangedIndex < 0)
                return;

            if (firstChangedIndex == 0)
                _initialHamsterOnBottom = hamsterOnBottom;

            if (firstChangedIndex < _previewSteps.Count)
                _previewSteps.RemoveRange(firstChangedIndex, _previewSteps.Count - firstChangedIndex);

            for (int i = firstChangedIndex; i < steps.Count; i++)
                _previewSteps.Add(new PreviewStep(steps[i]));

            _previewStartTime = Time.time;
        }

        public void ClearPreview()
        {
            _previewSteps.Clear();
        }

        public void EnsureGlMaterial()
        {
            if (_glMaterial != null)
                return;

            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
            {
                DebugManager.DiagLog("[BotV3 RENDER] Hidden/Internal-Colored shader not found");
                return;
            }

            _glMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _glMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _glMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _glMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            _glMaterial.SetInt("_ZWrite", 0);
            _glMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        }

        public void Dispose()
        {
            if (_glMaterial != null)
                Object.Destroy(_glMaterial);

            _glMaterial = null;
        }

        public void Render(Camera camera)
        {
            if (camera == null || _previewSteps.Count == 0)
                return;

            EnsureGlMaterial();
            if (_glMaterial == null)
                return;

            _glMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadProjectionMatrix(camera.projectionMatrix);
            GL.modelview = camera.worldToCameraMatrix;
            GL.Begin(GL.LINES);

            bool currentOnBottom = _initialHamsterOnBottom;
            float cumulativeWorldShiftBeforeStep = 0f;
            for (int i = 0; i < _previewSteps.Count; i++)
            {
                float alpha = Mathf.Max(MinTailAlpha, 1f - i * 0.22f);
                PreviewStep step = _previewSteps[i];
                DrawStepIndicator(step, currentOnBottom, cumulativeWorldShiftBeforeStep, alpha);
                currentOnBottom = GetLaneAfterStep(step, currentOnBottom);
                cumulativeWorldShiftBeforeStep += step.CompletionWorldShift;
            }

            GL.End();
            GL.PopMatrix();
        }

        private void DrawStepIndicator(
            PreviewStep step,
            bool hamsterOnBottom,
            float cumulativeWorldShiftBeforeStep,
            float alpha)
        {
            if (!TryResolveObstacleBounds(
                step,
                cumulativeWorldShiftBeforeStep,
                out float obstacleLeftX,
                out float obstacleRightX))
                return;

            float laneY = GetLaneY(hamsterOnBottom);
            float fireX = obstacleLeftX - step.ExecuteAtDistance;
            Color color = GetStepColor(step.Action);
            GL.Color(new Color(color.r, color.g, color.b, color.a * alpha));

            switch (step.Action)
            {
                case BotAction.SwitchLane:
                    DrawSwitchLaneGlyph(fireX, hamsterOnBottom);
                    break;

                case BotAction.Jump:
                default:
                    float endX = Mathf.Max(obstacleRightX + 1.6f, fireX + 2.0f);
                    DrawQuadraticArc(fireX, laneY, endX, laneY, 1.3f, out Vector3 preEnd);
                    DrawArrowhead(new Vector3(endX, laneY, 0f), preEnd, 0.4f);
                    break;
            }
        }

        private bool TryResolveObstacleBounds(
            PreviewStep step,
            float cumulativeWorldShiftBeforeStep,
            out float leftX,
            out float rightX)
        {
            var spawner = ObstacleSpawner.Instance;
            if (spawner != null)
            {
                var spawned = spawner.SpawnedObstacles;
                for (int i = 0; i < spawned.Count; i++)
                {
                    var instance = spawned[i];
                    if (instance?.ObstacleScript == null)
                        continue;

                    if (instance.ObstacleScript.GetInstanceID() != step.TargetObstacle.StableId)
                        continue;

                    float halfWidth = instance.ObstacleScript.ColliderWidth * 0.5f;
                    float centerX = instance.ObstacleScript.transform.position.x;
                    leftX = centerX - halfWidth;
                    rightX = centerX + halfWidth;
                    return true;
                }
            }

            float elapsedShift = Mathf.Max(0f, Time.time - _previewStartTime) * BotPhysicsConsts.GameSpeedBase;
            leftX = step.TargetObstacle.LeftX + cumulativeWorldShiftBeforeStep - elapsedShift;
            rightX = step.TargetObstacle.RightX + cumulativeWorldShiftBeforeStep - elapsedShift;
            return !float.IsNaN(leftX) &&
                   !float.IsInfinity(leftX) &&
                   !float.IsNaN(rightX) &&
                   !float.IsInfinity(rightX);
        }

        private static void DrawSwitchLaneGlyph(float xPos, bool hamsterOnBottom)
        {
            float fromY = GetLaneY(hamsterOnBottom);
            float toY = GetLaneY(!hamsterOnBottom);

            var from = new Vector3(xPos, fromY, 0f);
            var to = new Vector3(xPos, toY, 0f);

            GL.Vertex(from);
            GL.Vertex(to);

            Vector3 direction = (to - from).normalized;
            GL.Vertex(to);
            GL.Vertex(to + (Vector3)(Quaternion.Euler(0f, 0f, 150f) * direction) * 0.35f);
            GL.Vertex(to);
            GL.Vertex(to + (Vector3)(Quaternion.Euler(0f, 0f, -150f) * direction) * 0.35f);

            GL.Vertex(new Vector3(xPos - 0.24f, fromY, 0f));
            GL.Vertex(new Vector3(xPos + 0.24f, fromY, 0f));
            GL.Vertex(new Vector3(xPos - 0.24f, toY, 0f));
            GL.Vertex(new Vector3(xPos + 0.24f, toY, 0f));
        }

        private static void DrawQuadraticArc(
            float startX,
            float startY,
            float endX,
            float endY,
            float arcHeight,
            out Vector3 preEndPoint)
        {
            const int segments = 14;

            float midX = (startX + endX) * 0.5f;
            float midY = Mathf.Max(startY, endY) + arcHeight;

            Vector3 previousPoint = new Vector3(startX, startY, 0f);
            preEndPoint = previousPoint;

            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                float oneMinusT = 1f - t;

                float x = oneMinusT * oneMinusT * startX +
                          2f * oneMinusT * t * midX +
                          t * t * endX;
                float y = oneMinusT * oneMinusT * startY +
                          2f * oneMinusT * t * midY +
                          t * t * endY;

                Vector3 point = new Vector3(x, y, 0f);
                GL.Vertex(previousPoint);
                GL.Vertex(point);

                if (i == segments - 1)
                    preEndPoint = previousPoint;

                previousPoint = point;
            }
        }

        private static void DrawArrowhead(Vector3 end, Vector3 approachPoint, float size)
        {
            Vector3 direction = (end - approachPoint).normalized;
            GL.Vertex(end);
            GL.Vertex(end + (Vector3)(Quaternion.Euler(0f, 0f, 150f) * direction) * size);
            GL.Vertex(end);
            GL.Vertex(end + (Vector3)(Quaternion.Euler(0f, 0f, -150f) * direction) * size);
        }

        private static float GetLaneY(bool hamsterOnBottom)
        {
            return hamsterOnBottom
                ? Assets.Scripts.Consts.ObstacleY1Pos + LaneYOffset
                : Assets.Scripts.Consts.ObstacleY0Pos + LaneYOffset;
        }

        private static Color GetStepColor(BotAction action)
        {
            switch (action)
            {
                case BotAction.SwitchLane:
                    return new Color(0.25f, 0.95f, 1f, 1f);
                case BotAction.Jump:
                    return new Color(1f, 0.95f, 0.2f, 1f);
                default:
                    return new Color(0.85f, 0.85f, 0.85f, 1f);
            }
        }

        private int FindFirstChangedIndex(IReadOnlyList<BranchStep> steps)
        {
            int commonCount = Mathf.Min(_previewSteps.Count, steps.Count);
            int index = 0;

            while (index < commonCount && _previewSteps[index].Matches(steps[index]))
                index++;

            bool hasStructuralChange = _previewSteps.Count != steps.Count || index < commonCount;
            return hasStructuralChange ? index : -1;
        }

        private static bool GetLaneAfterStep(PreviewStep step, bool currentOnBottom)
        {
            return step.Action == BotAction.SwitchLane
                ? !currentOnBottom
                : currentOnBottom;
        }

        private sealed class PreviewStep
        {
            public BotAction Action { get; }
            public ObstacleInfo TargetObstacle { get; }
            public float ExecuteAtDistance { get; }
            public float CompletionWorldShift { get; }

            public PreviewStep(BranchStep step)
            {
                Action = step.Action;
                TargetObstacle = step.TargetObstacle;
                ExecuteAtDistance = step.ExecuteAtDistance;
                CompletionWorldShift = step.CompletionWorldShift;
            }

            public bool Matches(BranchStep step)
            {
                return Action == step.Action &&
                       TargetObstacle.StableId == step.TargetObstacle.StableId;
            }
        }
    }
}
