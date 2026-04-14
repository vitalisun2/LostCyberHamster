using System.Text;
using Assets.Scripts.Gameplay;
using UnityEngine;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Отрисовка HUD бота (OnGUI). Обычный класс — не MonoBehaviour.
    /// Оркестратор создаёт экземпляр и вызывает Draw() из своего OnGUI.
    /// Вся логика форматирования текста — здесь.
    /// </summary>
    public class BotHud
    {
        private readonly BotOrchestrator _orchestrator;
        private readonly ObjectClassifier _classifier = new ObjectClassifier();
        private readonly StringBuilder _sb = new StringBuilder(128);
        private GUIStyle _style;

        public BotHud(BotOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;
        }

        /// <summary>
        /// Отрисовывает HUD-панель бота, если бот включён.
        /// </summary>
        public void Draw()
        {
            if (!_orchestrator.IsEnabled)
                return;

            // Ленивая инициализация стиля
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 20,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = Color.white }
                };
            }

            // Форматировать и отрисовать
            string text = FormatHudText();
            var rect = new Rect(20f, 20f, 760f, 100f);
            GUI.Box(rect, text, _style);
        }

        /// <summary>
        /// Форматирует строку HUD: lane, energy, threats, plan.
        /// </summary>
        private string FormatHudText()
        {
            if (!_orchestrator.Initialized)
                return "Bot | Waiting for init...";

            // Базовая информация о хомяке
            Hamster hamster = _orchestrator.Hamster;
            string lane = hamster.IsOnBottomLine.Value ? "bottom" : "top";

            _sb.Clear();
            _sb.Append($"Bot | lane={lane} energy={hamster.Energy.Value} lives={hamster.Lives.Value}");

            // Количество угроз из snapshot
            var snapshot = _orchestrator.LastSnapshot;
            if (snapshot != null)
            {
                int threats = 0;
                for (int i = 0; i < snapshot.VisibleObjects.Count; i++)
                {
                    if (_classifier.IsThreat(snapshot.VisibleObjects[i], snapshot))
                        threats++;
                }
                _sb.Append($" | T:{threats}");
            }

            // Текущий план
            var plan = _orchestrator.Plan;
            if (plan.IsEmpty)
            {
                _sb.Append(" | Plan: none");
            }
            else
            {
                _sb.Append(" |");
                for (int i = 0; i < plan.Steps.Count; i++)
                {
                    if (i > 0) _sb.Append(" ->");
                    _sb.Append($" {plan.Steps[i].Action}");
                }
            }

            return _sb.ToString();
        }
    }
}
