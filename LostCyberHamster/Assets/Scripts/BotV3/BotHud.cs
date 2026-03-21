using Assets.Scripts.Gameplay;
using UnityEngine;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Отрисовка HUD бота (OnGUI). Обычный класс — не MonoBehaviour.
    /// Оркестратор создаёт экземпляр и вызывает Draw() из своего OnGUI.
    /// Вся логика форматирования текста — здесь.
    /// </summary>
    public class BotHud
    {
        private readonly BotOrchestrator _orchestrator;
        private GUIStyle _style;

        public BotHud(BotOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;
        }

        public void Draw()
        {
            if (!_orchestrator.IsEnabled)
                return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 20,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = Color.white }
                };
            }

            string text = FormatHudText();
            var rect = new Rect(20f, 20f, 760f, 100f);
            GUI.Box(rect, text, _style);
        }

        private string FormatHudText()
        {
            if (!_orchestrator.Initialized)
                return "BotV3 | Waiting for init...";

            Hamster hamster = _orchestrator.Hamster;
            string lane = hamster.IsOnBottomLine.Value ? "bottom" : "top";
            return $"BotV3 | lane={lane} energy={hamster.Energy.Value} lives={hamster.Lives.Value} | Plan: none";
        }
    }
}
