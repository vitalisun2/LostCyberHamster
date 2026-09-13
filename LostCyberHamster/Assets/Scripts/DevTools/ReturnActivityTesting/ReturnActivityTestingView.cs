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
        private readonly Button _inspect;
        private readonly ReturnActivityTestingRunner _runner = ReturnActivityTestingRunner.Shared;

        public ReturnActivityTestingView(Transform parent, DevToolsUiFactory factory)
        {
            Transform sessionCard = factory.CreateCard(
                "ReturnActivitiesSessionCard",
                parent,
                DevToolsTheme.Surface);
            factory.CreateSectionHeading("ReturnActivitiesHeading", sessionCard, "АКТИВНОСТИ / RETURN");
            factory.CreateBodyText(
                "ReturnActivitiesDescription",
                sessionCard,
                "Те же production-команды активностей, что и в Tools/Testing, но собранные по смысловым блокам.");
            _begin = factory.CreateButton("ReturnBegin", sessionCard, "Начать изолированную сессию активностей", DevToolsTheme.Button, _runner.Begin);
            _restore = factory.CreateButton("ReturnRestore", sessionCard, "Вернуть исходное сохранение", DevToolsTheme.Button, _runner.Restore);

            Transform mutationsCard = factory.CreateCard(
                "ReturnActivitiesMutationsCard",
                parent,
                DevToolsTheme.Surface);
            factory.CreateSectionHeading("ReturnActivitiesMutationsHeading", mutationsCard, "MUTATIONS");
            _mutations = new[]
            {
                factory.CreateButton("ReturnWin", mutationsCard, "Победа (без XP)", DevToolsTheme.Button, _runner.Win),
                factory.CreateButton("ReturnDay", mutationsCard, "Следующий локальный день", DevToolsTheme.Button, _runner.NextDay),
                factory.CreateButton("ReturnBackDay", mutationsCard, "Предыдущий локальный день", DevToolsTheme.Button, _runner.PreviousDay),
                factory.CreateButton("ReturnWeek", mutationsCard, "+7 локальных дней", DevToolsTheme.Button, _runner.NextWeek),
                factory.CreateButton("ReturnClaim", mutationsCard, "Claim одной награды", DevToolsTheme.Button, _runner.Claim)
            };

            Transform inspectCard = factory.CreateCard(
                "ReturnActivitiesInspectCard",
                parent,
                DevToolsTheme.StatusCard);
            factory.CreateSectionHeading("ReturnActivitiesInspectHeading", inspectCard, "SNAPSHOT");
            _inspect = factory.CreateButton("ReturnInspect", inspectCard, "Прочитать активности", DevToolsTheme.Button, _runner.Inspect);
            _status = factory.CreateBodyText("ReturnStatus", inspectCard, _runner.Status);
        }

        public void Render()
        {
            _begin.interactable = _runner.CanBegin;
            _restore.interactable = GameManagement.GameDataManager.HasProgressionTestingBackup;
            foreach (var button in _mutations) button.interactable = _runner.CanChange;
            _inspect.interactable = true;
            _status.text = _runner.Status;
        }
    }
}
#endif
