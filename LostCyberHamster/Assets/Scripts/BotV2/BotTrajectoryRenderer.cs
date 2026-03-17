using Assets.Scripts.Common.Models;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using UnityEngine;

namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Рендерит GL-кривые траектории запланированных шагов бота.
    /// Не является MonoBehaviour — вызывается из BotOrchestrator.OnRenderObject().
    /// </summary>
    internal sealed class BotTrajectoryRenderer
    {
        private Material _glMaterial;

        private ChainStep _previewFirstStep;
        private ChainStep _previewSecondStep;
        private bool _previewStep1LaneAfter;
        private bool _previewSecondUsesProjectedCoordinates;

        public bool HasPreview    => _previewFirstStep != null;
        public ChainStep PreviewFirst  => _previewFirstStep;
        public ChainStep PreviewSecond => _previewSecondStep;

        public void UpdatePreview(ChainStep first, ChainStep second, bool step1LaneAfter, bool secondUsesProjectedCoordinates)
        {
            _previewFirstStep = first;
            _previewSecondStep = second;
            _previewStep1LaneAfter = step1LaneAfter;
            _previewSecondUsesProjectedCoordinates = secondUsesProjectedCoordinates;
        }

        public void ClearPreview()
        {
            _previewFirstStep = null;
            _previewSecondStep = null;
            _previewSecondUsesProjectedCoordinates = false;
        }

        /// <summary>
        /// Вызывать из MonoBehaviour.OnRenderObject() при включённом флаге отрисовки.
        /// </summary>
        public void Render(Camera cam, bool hamOnBottom)
        {
            if (_glMaterial == null || _previewFirstStep == null) return;

            _glMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadProjectionMatrix(cam.projectionMatrix);
            GL.modelview = cam.worldToCameraMatrix;
            GL.Begin(GL.LINES);

            DrawStepIndicator(_previewFirstStep, hamOnBottom, useLiveObstaclePosition: true, stepAlpha: 1.0f);
            if (_previewSecondStep != null)
                DrawStepIndicator(
                    _previewSecondStep,
                    _previewStep1LaneAfter,
                    useLiveObstaclePosition: !_previewSecondUsesProjectedCoordinates,
                    stepAlpha: 0.6f);

            GL.End();
            GL.PopMatrix();
        }

        public void EnsureGLMaterial()
        {
            if (_glMaterial != null) return;

            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
            {
                DebugManager.DiagLog("[BotTrajectoryRenderer] Hidden/Internal-Colored shader not found!");
                return;
            }

            _glMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _glMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _glMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _glMaterial.SetInt("_Cull",     (int)UnityEngine.Rendering.CullMode.Off);
            _glMaterial.SetInt("_ZWrite",   0);
            _glMaterial.SetInt("_ZTest",    (int)UnityEngine.Rendering.CompareFunction.Always);

            DebugManager.DiagLog("[BotTrajectoryRenderer] GL material created (ZTest=Always)");
        }

        public void Dispose()
        {
            if (_glMaterial != null)
                Object.Destroy(_glMaterial);
            _glMaterial = null;
        }

        private enum StepVisualKind
        {
            SwitchLane,
            JumpOver,
            SuperJumpOver,
            JumpOnBounce
        }

        private static void DrawStepIndicator(
            ChainStep step,
            bool hamOnBottom,
            bool useLiveObstaclePosition,
            float stepAlpha = 1.0f)
        {
            // Resolve live obstacle position. If obstacle is no longer spawned,
            // skip drawing entirely to avoid a frozen artefact.
            float obsLeft  = step.TargetObstacle.LeftX;
            float obsRight = step.TargetObstacle.RightX;
            bool  obsFound = !useLiveObstaclePosition;

            if (useLiveObstaclePosition)
            {
                var spawner = ObstacleSpawner.Instance;
                if (spawner == null)
                    return;

                var spawned = spawner.SpawnedObstacles;
                for (int i = 0; i < spawned.Count; i++)
                {
                    var inst = spawned[i];
                    if (inst?.ObstacleScript == null) continue;
                    if (inst.ObstacleScript.GetInstanceID() != step.TargetObstacle.StableId) continue;
                    float hw = inst.ObstacleScript.ColliderWidth * 0.5f;
                    float cx = inst.ObstacleScript.transform.position.x;
                    obsLeft  = cx - hw;
                    obsRight = cx + hw;
                    obsFound = true;
                    break;
                }
            }

            // Obstacle despawned — do not draw with stale coordinates
            if (!obsFound) return;

            float laneY = hamOnBottom
                ? Assets.Scripts.Consts.ObstacleY1Pos + 0.95f
                : Assets.Scripts.Consts.ObstacleY0Pos + 0.95f;

            float fireX = obsLeft - step.ExecuteAtDistance;
            StepVisualKind visualKind = ResolveVisualKind(step);

            Color c = GetStepColor(step.Action);
            GL.Color(new Color(c.r, c.g, c.b, c.a * stepAlpha));

            switch (visualKind)
            {
                case StepVisualKind.SwitchLane:
                    DrawSwitchLaneGlyph(fireX, laneY, hamOnBottom);
                    break;

                case StepVisualKind.SuperJumpOver:
                {
                    float endX = Mathf.Max(obsRight + 2.4f, fireX + 2.8f);
                    DrawQuadraticArc(fireX, laneY, endX, laneY, arcHeight: 2.0f, out Vector3 preEnd);
                    DrawArrowhead(new Vector3(endX, laneY, 0f), preEnd, 0.45f);
                    break;
                }

                case StepVisualKind.JumpOnBounce:
                {
                    float bounceX = Mathf.Max(obsLeft + 0.55f, fireX + 0.9f);
                    float endX = Mathf.Max(obsRight + 1.3f, bounceX + 1.0f);
                    float bounceY = laneY + 0.95f;

                    DrawQuadraticArc(fireX, laneY, bounceX, bounceY, arcHeight: 1.0f, out _);
                    DrawQuadraticArc(bounceX, bounceY, endX, laneY, arcHeight: 0.95f, out Vector3 preEnd);
                    DrawArrowhead(new Vector3(endX, laneY, 0f), preEnd, 0.40f);
                    break;
                }

                default:
                {
                    float endX = Mathf.Max(obsRight + 1.6f, fireX + 2.0f);
                    DrawQuadraticArc(fireX, laneY, endX, laneY, arcHeight: 1.3f, out Vector3 preEnd);
                    DrawArrowhead(new Vector3(endX, laneY, 0f), preEnd, 0.40f);
                    break;
                }
            }
        }

        private static StepVisualKind ResolveVisualKind(ChainStep step)
        {
            switch (step.Semantic)
            {
                case StepSemantic.SwitchLane:
                    return StepVisualKind.SwitchLane;

                case StepSemantic.SuperJumpOver:
                    return StepVisualKind.SuperJumpOver;

                case StepSemantic.JumpOnBounce:
                case StepSemantic.JumpOnRoof:
                    return StepVisualKind.JumpOnBounce;

                default:
                    return StepVisualKind.JumpOver;
            }
        }

        private static void DrawSwitchLaneGlyph(float xPos, float laneY, bool hamOnBottom)
        {
            float topY    = Assets.Scripts.Consts.ObstacleY0Pos + 0.95f;
            float botY    = Assets.Scripts.Consts.ObstacleY1Pos + 0.95f;
            float targetY = hamOnBottom ? topY : botY;

            var from = new Vector3(xPos, laneY, 0f);
            var to = new Vector3(xPos, targetY, 0f);

            GL.Vertex(from);
            GL.Vertex(to);

            Vector3 dir = (to - from).normalized;
            GL.Vertex(to);
            GL.Vertex(to + (Vector3)(Quaternion.Euler(0f, 0f, 150f) * dir) * 0.35f);
            GL.Vertex(to);
            GL.Vertex(to + (Vector3)(Quaternion.Euler(0f, 0f, -150f) * dir) * 0.35f);

            GL.Vertex(new Vector3(xPos - 0.24f, laneY, 0f));
            GL.Vertex(new Vector3(xPos + 0.24f, laneY, 0f));
            GL.Vertex(new Vector3(xPos - 0.24f, targetY, 0f));
            GL.Vertex(new Vector3(xPos + 0.24f, targetY, 0f));
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

            Vector3 prev = new Vector3(startX, startY, 0f);
            preEndPoint = prev;

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

                Vector3 pt = new Vector3(x, y, 0f);
                GL.Vertex(prev);
                GL.Vertex(pt);

                if (i == segments - 1)
                    preEndPoint = prev;

                prev = pt;
            }
        }

        private static void DrawArrowhead(Vector3 end, Vector3 approachPoint, float size)
        {
            Vector3 dir = (end - approachPoint).normalized;
            GL.Vertex(end); GL.Vertex(end + (Vector3)(Quaternion.Euler(0f, 0f,  150f) * dir) * size);
            GL.Vertex(end); GL.Vertex(end + (Vector3)(Quaternion.Euler(0f, 0f, -150f) * dir) * size);
        }

        private static Color GetStepColor(BotAction action)
        {
            switch (action)
            {
                case BotAction.SwitchLane:
                    return new Color(0.25f, 0.95f, 1f, 1f);
                case BotAction.Jump:
                    return new Color(1f, 0.95f, 0.2f, 1f);
                case BotAction.SuperJump:
                    return new Color(1f, 0.55f, 0.2f, 1f);
                default:
                    return new Color(0.8f, 0.8f, 0.8f, 0.95f);
            }
        }
    }
}
