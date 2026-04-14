using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Perception;
using UnityEngine;

namespace Assets.Scripts.Bot
{
    internal sealed class BotPlanRenderer
    {
        private const float LaneYOffset = 0.95f;
        private const float MinTailAlpha = 0.3f;

        private Material _glMaterial;

        public void Render(
            BotPlan plan,
            BotPerceptionSnapshot snapshot,
            bool initialBottomLine,
            bool hideHeadAction,
            Camera camera)
        {
            if (plan == null || !plan.HasActions || snapshot == null || camera == null)
                return;

            EnsureMaterial();
            if (_glMaterial == null)
                return;

            _glMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadProjectionMatrix(camera.projectionMatrix);
            GL.modelview = camera.worldToCameraMatrix;
            GL.Begin(GL.LINES);

            bool currentBottomLine = initialBottomLine;
            int startIndex = hideHeadAction ? 1 : 0;
            for (int index = startIndex; index < plan.Actions.Count; index++)
            {
                PlannedAction action = plan.Actions[index];
                float alpha = Mathf.Max(MinTailAlpha, 1f - (index - startIndex) * 0.22f);
                DrawAction(action, snapshot, currentBottomLine, alpha);

                if (action.TargetBottomLine.HasValue)
                    currentBottomLine = action.TargetBottomLine.Value;
            }

            GL.End();
            GL.PopMatrix();
        }

        public void Dispose()
        {
            if (_glMaterial != null)
                Object.Destroy(_glMaterial);

            _glMaterial = null;
        }

        private void DrawAction(
            PlannedAction action,
            BotPerceptionSnapshot snapshot,
            bool currentBottomLine,
            float alpha)
        {
            switch (action.Kind)
            {
                case BotActionKind.Tap:
                    DrawSwitchLaneGlyph(ResolveRenderX(action, snapshot), currentBottomLine, alpha);
                    break;
            }
        }

        private static float ResolveRenderX(PlannedAction action, BotPerceptionSnapshot snapshot)
        {
            if (action.TargetObstacleInstanceId.HasValue)
            {
                for (int obstacleIndex = 0; obstacleIndex < snapshot.VisibleObstacles.Count; obstacleIndex++)
                {
                    VisibleObstacleSnapshot obstacle = snapshot.VisibleObstacles[obstacleIndex];
                    if (obstacle.InstanceId == action.TargetObstacleInstanceId.Value)
                        return obstacle.LeftX;
                }
            }

            return action.TriggerX;
        }

        private static void DrawSwitchLaneGlyph(float triggerX, bool currentBottomLine, float alpha)
        {
            float fromY = GetLaneY(currentBottomLine);
            float toY = GetLaneY(!currentBottomLine);
            Color color = new Color(0.25f, 0.95f, 1f, alpha);
            GL.Color(color);

            Vector3 from = new Vector3(triggerX, fromY, 0f);
            Vector3 to = new Vector3(triggerX, toY, 0f);
            Vector3 direction = (to - from).normalized;

            GL.Vertex(from);
            GL.Vertex(to);

            GL.Vertex(to);
            GL.Vertex(to + (Vector3)(Quaternion.Euler(0f, 0f, 150f) * direction) * 0.35f);
            GL.Vertex(to);
            GL.Vertex(to + (Vector3)(Quaternion.Euler(0f, 0f, -150f) * direction) * 0.35f);

            GL.Vertex(new Vector3(triggerX - 0.24f, fromY, 0f));
            GL.Vertex(new Vector3(triggerX + 0.24f, fromY, 0f));
            GL.Vertex(new Vector3(triggerX - 0.24f, toY, 0f));
            GL.Vertex(new Vector3(triggerX + 0.24f, toY, 0f));
        }

        private void EnsureMaterial()
        {
            if (_glMaterial != null)
                return;

            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
                return;

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

        private static float GetLaneY(bool hamsterOnBottom)
        {
            return hamsterOnBottom
                ? Assets.Scripts.Consts.ObstacleY1Pos + LaneYOffset
                : Assets.Scripts.Consts.ObstacleY0Pos + LaneYOffset;
        }
    }
}
