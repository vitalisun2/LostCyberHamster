using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;
using UnityEngine;

namespace Assets.Scripts.Bot.Diagnostics
{
    /// <summary>
    /// Рисует текущий план бота поверх игрового мира для отладки.
    /// </summary>
    internal sealed class BotPlanRenderer
    {
        private const float LaneYOffset = 0.95f;
        private const float MinTailAlpha = 0.3f;
        private const float SuperJumpWidth = 1.15f;
        private const float SuperJumpHeight = 1.45f;

        private readonly List<RenderCommand> _renderCommands = new List<RenderCommand>();

        private Material _glMaterial;

        /// <summary>
        /// Отрисовывает последовательность действий текущего плана.
        /// </summary>
        public void Render(
            BotPlan plan,
            WorldSnapshot snapshot,
            bool initialBottomLine,
            bool hideHeadAction,
            Camera camera)
        {
            if (plan == null || !plan.HasActions || snapshot == null || camera == null)
            {
                _renderCommands.Clear();
                return;
            }

            BuildRenderCommands(plan, snapshot, initialBottomLine, hideHeadAction, _renderCommands);
            if (_renderCommands.Count == 0)
                return;

            EnsureMaterial();
            if (_glMaterial == null)
                return;

            _glMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadProjectionMatrix(camera.projectionMatrix);
            GL.modelview = camera.worldToCameraMatrix;
            GL.Begin(GL.LINES);

            for (int commandIndex = 0; commandIndex < _renderCommands.Count; commandIndex++)
                DrawCommand(_renderCommands[commandIndex]);

            GL.End();
            GL.PopMatrix();
        }

        private static void BuildRenderCommands(
            BotPlan plan,
            WorldSnapshot snapshot,
            bool initialBottomLine,
            bool hideHeadAction,
            List<RenderCommand> renderCommands)
        {
            renderCommands.Clear();

            bool currentBottomLine = initialBottomLine;
            int startIndex = hideHeadAction ? 1 : 0;
            if (hideHeadAction && plan.Actions[0].TargetBottomLine.HasValue)
                currentBottomLine = plan.Actions[0].TargetBottomLine.Value;

            for (int index = startIndex; index < plan.Actions.Count; index++)
            {
                PlannedAction action = plan.Actions[index];
                float alpha = Mathf.Max(MinTailAlpha, 1f - (index - startIndex) * 0.22f);
                if (TryBuildRenderCommand(action, snapshot, currentBottomLine, alpha, out RenderCommand command))
                    renderCommands.Add(command);

                if (action.TargetBottomLine.HasValue)
                    currentBottomLine = action.TargetBottomLine.Value;
            }
        }

        /// <summary>
        /// Освобождает временные ресурсы рендерера.
        /// </summary>
        public void Dispose()
        {
            if (_glMaterial != null)
                UnityEngine.Object.Destroy(_glMaterial);

            _glMaterial = null;
            _renderCommands.Clear();
        }

        /// <summary>
        /// Собирает render-команду одного действия.
        /// </summary>
        private static bool TryBuildRenderCommand(
            PlannedAction action,
            WorldSnapshot snapshot,
            bool currentBottomLine,
            float alpha,
            out RenderCommand command)
        {
            command = default;

            switch (action.Kind)
            {
                case BotActionKind.SwitchLane:
                    if (!TryGetRenderWorldX(action, snapshot, out float switchLaneRenderWorldX))
                        return false;

                    command = new RenderCommand(
                        BotActionKind.SwitchLane,
                        switchLaneRenderWorldX,
                        currentBottomLine,
                        alpha);
                    return true;

                case BotActionKind.SuperJumpOver:
                case BotActionKind.SuperJumpOnRoof:
                    if (!TryGetRenderWorldX(action, snapshot, out float superJumpRenderWorldX))
                        return false;

                    command = new RenderCommand(
                        action.Kind,
                        superJumpRenderWorldX,
                        currentBottomLine,
                        alpha);
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Возвращает мировую X-координату для рендера действия.
        /// </summary>
        private static bool TryGetRenderWorldX(
            PlannedAction action,
            WorldSnapshot snapshot,
            out float renderWorldX)
        {
            if (action.TargetObstacleInstanceId.HasValue)
            {
                for (int obstacleIndex = 0; obstacleIndex < snapshot.Obstacles.Count; obstacleIndex++)
                {
                    ObstacleSnapshot obstacle = snapshot.Obstacles[obstacleIndex];
                    if (obstacle.InstanceId != action.TargetObstacleInstanceId.Value)
                        continue;

                    float remainingWorldShift = obstacle.LeftX - action.TriggerX;
                    if (remainingWorldShift < 0f)
                    {
                        renderWorldX = 0f;
                        return false;
                    }

                    // Convert remaining world travel into the current road point
                    // that will be under the hamster when the action starts.
                    renderWorldX = snapshot.Hamster.CenterX + remainingWorldShift;
                    return true;
                }

                renderWorldX = 0f;
                return false;
            }

            renderWorldX = action.RenderWorldX;
            return true;
        }

        private static void DrawCommand(RenderCommand command)
        {
            switch (command.Kind)
            {
                case BotActionKind.SwitchLane:
                    DrawSwitchLaneGlyph(command.WorldX, command.CurrentBottomLine, command.Alpha);
                    break;
                case BotActionKind.SuperJumpOver:
                case BotActionKind.SuperJumpOnRoof:
                    DrawSuperJumpGlyph(command.WorldX, command.CurrentBottomLine, command.Alpha);
                    break;
            }
        }

        private readonly struct RenderCommand
        {
            public RenderCommand(BotActionKind kind, float worldX, bool currentBottomLine, float alpha)
            {
                Kind = kind;
                WorldX = worldX;
                CurrentBottomLine = currentBottomLine;
                Alpha = alpha;
            }

            public BotActionKind Kind { get; }
            public float WorldX { get; }
            public bool CurrentBottomLine { get; }
            public float Alpha { get; }
        }

        /// <summary>
        /// Рисует глиф смены линии.
        /// </summary>
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

        /// <summary>
        /// Рисует глиф суперпрыжка.
        /// </summary>
        private static void DrawSuperJumpGlyph(float triggerX, bool currentBottomLine, float alpha)
        {
            float laneY = GetLaneY(currentBottomLine);
            float halfWidth = SuperJumpWidth * 0.5f;

            Vector3 start = new Vector3(triggerX - halfWidth, laneY, 0f);
            Vector3 midLeft = new Vector3(triggerX - halfWidth * 0.35f, laneY + SuperJumpHeight * 0.72f, 0f);
            Vector3 apex = new Vector3(triggerX, laneY + SuperJumpHeight, 0f);
            Vector3 midRight = new Vector3(triggerX + halfWidth * 0.4f, laneY + SuperJumpHeight * 0.68f, 0f);
            Vector3 end = new Vector3(triggerX + halfWidth, laneY, 0f);
            Vector3 landingLeft = new Vector3(triggerX + halfWidth - 0.2f, laneY + 0.18f, 0f);
            Vector3 landingRight = new Vector3(triggerX + halfWidth + 0.05f, laneY + 0.18f, 0f);
            Vector3 apexMarkerTop = new Vector3(triggerX, laneY + SuperJumpHeight + 0.35f, 0f);

            Color color = new Color(1f, 0.78f, 0.18f, alpha);
            GL.Color(color);

            GL.Vertex(start);
            GL.Vertex(midLeft);
            GL.Vertex(midLeft);
            GL.Vertex(apex);
            GL.Vertex(apex);
            GL.Vertex(midRight);
            GL.Vertex(midRight);
            GL.Vertex(end);

            GL.Vertex(new Vector3(triggerX - 0.18f, laneY, 0f));
            GL.Vertex(new Vector3(triggerX + 0.18f, laneY, 0f));

            GL.Vertex(end);
            GL.Vertex(landingLeft);
            GL.Vertex(end);
            GL.Vertex(landingRight);

            GL.Vertex(apex);
            GL.Vertex(apexMarkerTop);
            GL.Vertex(new Vector3(triggerX - 0.18f, laneY + SuperJumpHeight + 0.16f, 0f));
            GL.Vertex(new Vector3(triggerX + 0.18f, laneY + SuperJumpHeight + 0.16f, 0f));
        }

        /// <summary>
        /// Создаёт GL-материал для рендера.
        /// </summary>
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

        /// <summary>
        /// Возвращает Y-координату линии.
        /// </summary>
        private static float GetLaneY(bool hamsterOnBottom)
        {
            return hamsterOnBottom
                ? Assets.Scripts.Consts.ObstacleY1Pos + LaneYOffset
                : Assets.Scripts.Consts.ObstacleY0Pos + LaneYOffset;
        }
    }
}
