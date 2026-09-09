#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Assets.Scripts.DevTools.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.DevTools.ReturnActivityTesting
{
    /// <summary>Добавляет компактные команды активностей в существующую DEV-страницу прогресса.</summary>
    internal sealed class ReturnActivityTestingView
    {
        private readonly Text _status;
        private readonly Button _begin;
        private readonly Button _restore;
        private readonly Button[] _mutations;
        private readonly ReturnActivityTestingRunner _runner = ReturnActivityTestingRunner.Shared;

        public ReturnActivityTestingView(Transform parent, DevToolsUiFactory factory)
        {
            factory.CreateSectionHeading("ReturnActivitiesHeading", parent, "АКТИВНОСТИ / RETURN");
            _begin = factory.CreateButton("ReturnBegin", parent, "Начать изолированную сессию активностей", DevToolsTheme.Button, _runner.Begin);
            _restore = factory.CreateButton("ReturnRestore", parent, "Вернуть исходное сохранение", DevToolsTheme.Button, _runner.Restore);
            _mutations = new[]
            {
                factory.CreateButton("ReturnWin", parent, "Победа (без XP)", DevToolsTheme.Button, _runner.Win),
                factory.CreateButton("ReturnDay", parent, "Следующий UTC-день", DevToolsTheme.Button, _runner.NextDay),
                factory.CreateButton("ReturnBackDay", parent, "Предыдущий UTC-день", DevToolsTheme.Button, _runner.PreviousDay),
                factory.CreateButton("ReturnWeek", parent, "+7 UTC-дней", DevToolsTheme.Button, _runner.NextWeek),
                factory.CreateButton("ReturnClaim", parent, "Claim одной награды", DevToolsTheme.Button, _runner.Claim)
            };
            factory.CreateButton("ReturnInspect", parent, "Прочитать активности", DevToolsTheme.Button, _runner.Inspect);
            _status = factory.CreateBodyText("ReturnStatus", parent, _runner.Status);
        }

        public void Render()
        {
            _begin.interactable = _runner.CanBegin;
            _restore.interactable = GameManagement.GameDataManager.HasProgressionTestingBackup;
            foreach (var button in _mutations) button.interactable = _runner.CanChange;
            _status.text = _runner.Status;
        }
    }
}
#endif
