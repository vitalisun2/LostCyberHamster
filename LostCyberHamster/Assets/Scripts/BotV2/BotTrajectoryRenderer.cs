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

        public bool HasPreview    => _previewFirstStep != null;
        public ChainStep PreviewFirst  => _previewFirstStep;
        public ChainStep PreviewSecond => _previewSecondStep;

        public void UpdatePreview(ChainStep first, ChainStep second, bool step1LaneAfter)
        {
            _previewFirstStep = first;
            _previewSecondStep = second;
            _previewStep1LaneAfter = step1LaneAfter;
        }

        public void ClearPreview()
        {
            _previewFirstStep = null;
            _previewSecondStep = null;
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

            DrawStepIndicator(_previewFirstStep, hamOnBottom, stepAlpha: 1.0f);
            if (_previewSecondStep != null)
                DrawStepIndicator(_previewSecondStep, _previewStep1LaneAfter, stepAlpha: 0.6f);

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

        // ─── Animation keyframe data ─── [time, y, outSlope, inSlope]
        // Source: transform_jump.anim (duration 1.0 s)
        private static readonly float[,] s_JumpKF =
        {
            {  0.000f,  0.000f,  7.477f,   0.000f },
            {  0.133f,  0.880f,  4.302f,   4.302f },
            {  0.433f,  1.424f,  0.184f,   0.184f },
            {  0.850f,  0.800f, -3.687f,  -3.687f },
            {  1.000f,  0.000f,  0.000f,  -6.478f },
        };
        // Source: transform_super_jump.anim (duration 1.2 s, starts elevated y=1.27)
        private static readonly float[,] s_SuperJumpKF =
        {
            {  0.000f,  1.273f,  6.081f,   0.000f  },
            {  0.183f,  2.139f,  3.021f,   3.021f  },
            {  0.583f,  2.619f, -0.498f,  -0.498f  },
            {  1.000f,  1.584f, -5.314f,  -5.314f  },
            {  1.200f,  0.000f,  0.000f, -10.197f  },
        };
        // Source: transform_jump_on.anim (duration 1.817 s, two-arc, tangent break at t=0.85)
        private static readonly float[,] s_JumpOnKF =
        {
            {  0.000f,  0.000f,  7.477f,   0.000f  },
            {  0.133f,  0.880f,  4.302f,   4.302f  },
            {  0.433f,  1.424f,  0.184f,   0.184f  },
            {  0.850f,  0.800f,  8.339f,  -2.670f  }, // tangent break: landing on obstacle
            {  1.017f,  1.912f,  4.663f,   4.663f  },
            {  1.150f,  2.316f,  1.619f,   1.619f  },
            {  1.283f,  2.400f, -0.162f,  -0.162f  },
            {  1.617f,  1.528f, -5.968f,  -5.968f  },
            {  1.817f,  0.000f,  0.000f,  -7.640f  }, // y=0 remapped to roofY
        };

        private static void DrawStepIndicator(ChainStep step, bool hamOnBottom, float stepAlpha = 1.0f)
        {
            const float JumpFireDist = 1.5f;

            // Resolve live obstacle position. If obstacle is no longer spawned,
            // skip drawing entirely to avoid a frozen artefact.
            var spawner = ObstacleSpawner.Instance;
            float obsLeft  = step.TargetObstacle.LeftX;
            float obsRight = step.TargetObstacle.RightX;
            bool  obsFound = spawner == null; // proceed with snapshot when there is no spawner

            if (spawner != null)
            {
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
            float roofY = hamOnBottom
                ? Assets.Scripts.Consts.ObstacleRoofY1Pos + 0.15f
                : Assets.Scripts.Consts.ObstacleRoofY0Pos + 0.15f;

            Color c = GetStepColor(step.Action);
            GL.Color(new Color(c.r, c.g, c.b, c.a * stepAlpha));

            bool isJumpOn = step.Action == BotAction.Jump &&
                            (step.TargetObstacle.Category == ObjectCategory.Target ||
                             step.TargetObstacle.Type == ObstacleTypeEnum.bigNotAlive ||
                             step.TargetObstacle.Type == ObstacleTypeEnum.mediumNotAlive);

            float speed = Assets.Scripts.Consts.GameSpeedBase;

            if (step.Action == BotAction.SuperJump)
            {
                float fireX = obsLeft - JumpFireDist;
                float endX  = fireX + s_SuperJumpKF[s_SuperJumpKF.GetLength(0) - 1, 0] * speed;
                DrawHermiteTrajectory(s_SuperJumpKF, fireX, endX, laneY, laneY, 8, out Vector3 preEnd);
                DrawArrowhead(new Vector3(endX, laneY, 0f), preEnd, 0.45f);
            }
            else if (isJumpOn)
            {
                float fireX = obsLeft - JumpFireDist;
                float endX  = fireX + s_JumpOnKF[s_JumpOnKF.GetLength(0) - 1, 0] * speed;
                DrawHermiteTrajectory(s_JumpOnKF, fireX, endX, laneY, roofY, 8, out Vector3 preEnd);
                DrawArrowhead(new Vector3(endX, roofY, 0f), preEnd, 0.40f);
            }
            else if (step.Action == BotAction.Jump)
            {
                float fireX = obsLeft - JumpFireDist;
                float endX  = fireX + s_JumpKF[s_JumpKF.GetLength(0) - 1, 0] * speed;
                DrawHermiteTrajectory(s_JumpKF, fireX, endX, laneY, laneY, 8, out Vector3 preEnd);
                DrawArrowhead(new Vector3(endX, laneY, 0f), preEnd, 0.40f);
            }
            else if (step.Action == BotAction.SwitchLane)
            {
                float topY    = Assets.Scripts.Consts.ObstacleY0Pos + 0.95f;
                float botY    = Assets.Scripts.Consts.ObstacleY1Pos + 0.95f;
                float targetY = hamOnBottom ? topY : botY;
                float xPos    = obsLeft - 0.2f;
                var from = new Vector3(xPos, laneY,   0f);
                var to   = new Vector3(xPos, targetY, 0f);
                GL.Vertex(from); GL.Vertex(to);
                Vector3 dir = (to - from).normalized;
                GL.Vertex(to); GL.Vertex(to + (Vector3)(Quaternion.Euler(0f, 0f,  150f) * dir) * 0.35f);
                GL.Vertex(to); GL.Vertex(to + (Vector3)(Quaternion.Euler(0f, 0f, -150f) * dir) * 0.35f);
                GL.Vertex(new Vector3(xPos - 0.25f, laneY,   0f)); GL.Vertex(new Vector3(xPos + 0.25f, laneY,   0f));
                GL.Vertex(new Vector3(xPos - 0.25f, targetY, 0f)); GL.Vertex(new Vector3(xPos + 0.25f, targetY, 0f));
            }
        }

        /// <summary>
        /// Рисует кубическую Hermite-кривую через ключевые кадры анимации.
        /// kf: строки = {time, y, outSlope, inSlope}. X параметризован через gameSpeed.
        /// Последний кадр y заменяется landingY (для точного приземления).
        /// </summary>
        private static void DrawHermiteTrajectory(
            float[,] kf, float startX, float endX,
            float baseY, float landingY,
            int segsPerSpan, out Vector3 preEndPoint)
        {
            int   n      = kf.GetLength(0);
            float tMax   = kf[n - 1, 0];
            float xSpan  = endX - startX;
            float yLand  = landingY - baseY;

            preEndPoint = new Vector3(startX, baseY, 0f);

            for (int i = 0; i < n - 1; i++)
            {
                float t0   = kf[i,   0];
                float y0   = kf[i,   1];
                float oSlp = kf[i,   2];  // outSlope (departure tangent)
                float t1   = kf[i+1, 0];
                float y1   = kf[i+1, 1];
                float iSlp = kf[i+1, 3];  // inSlope (arrival tangent)
                bool  last = i == n - 2;

                float yEnd = last ? yLand : y1;
                float dt   = t1 - t0;
                float m0   = oSlp * dt;
                float m1   = iSlp * dt;

                Vector3 prev = new Vector3(startX + (t0 / tMax) * xSpan, baseY + y0, 0f);

                for (int j = 1; j <= segsPerSpan; j++)
                {
                    float s   = j / (float)segsPerSpan;
                    float s2  = s * s;
                    float s3  = s2 * s;
                    float yVal = (2*s3 - 3*s2 + 1)*y0 + (s3 - 2*s2 + s)*m0
                               + (-2*s3 + 3*s2)*yEnd + (s3 - s2)*m1;
                    float t   = t0 + s * dt;
                    Vector3 pt = new Vector3(startX + (t / tMax) * xSpan, baseY + yVal, 0f);

                    GL.Vertex(prev);
                    GL.Vertex(pt);

                    if (last && j == segsPerSpan - 1)
                        preEndPoint = prev;

                    prev = pt;
                }
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
