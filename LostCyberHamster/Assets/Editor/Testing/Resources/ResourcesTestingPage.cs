using System;
using GameManagement;
using UnityEditor;
using UnityEngine;
using Vues.GameCore;

namespace LostCyberHamster.Editor.Testing.Resources
{
    /// <summary>Рисует точное DEV-начисление Money внутри Tools/Testing.</summary>
    internal sealed class ResourcesTestingPage : IDisposable
    {
        private const int _defaultAmount = 100;

        private readonly Action _repaint;
        private int _amount = _defaultAmount;
        private string _status = "Укажите Amount и нажмите Add Money.";
        private MessageType _statusType = MessageType.Info;

        public ResourcesTestingPage(Action repaint)
        {
            _repaint = repaint;
        }

        public void Draw(Action navigateBack)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Back", GUILayout.Width(70f)))
                    navigateBack?.Invoke();
                EditorGUILayout.LabelField("Resources", EditorStyles.boldLabel);
            }

            EditorGUILayout.Space(8f);
            bool isReady = EditorApplication.isPlaying && ResourceManager.IsReady;
            int previousAmount = _amount;
            using (new EditorGUI.DisabledScope(!isReady))
                _amount = EditorGUILayout.IntField("Amount", _amount);
            if (_amount != previousAmount)
            {
                _status = "Укажите Amount и нажмите Add Money.";
                _statusType = MessageType.Info;
            }

            int balance = isReady
                ? ResourceManager.GetCurrentBalance(ResourceType.Coins)
                : 0;
            bool amountValid = _amount > 0 && balance <= int.MaxValue - _amount;
            using (new EditorGUI.DisabledScope(!isReady || !amountValid))
            {
                if (GUILayout.Button("Add Money"))
                    AddMoney();
            }

            if (!isReady)
            {
                EditorGUILayout.HelpBox(
                    "Resources доступны в Play Mode после загрузки PlayerData.",
                    MessageType.Info);
            }
            else if (_amount <= 0)
            {
                EditorGUILayout.HelpBox("Amount должен быть больше 0.", MessageType.Warning);
            }
            else if (!amountValid)
            {
                EditorGUILayout.HelpBox("Amount переполняет Money balance.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(_status, _statusType);
            }
        }

        public void Dispose()
        {
        }

        private void AddMoney()
        {
            bool added = ResourceManager.TryAddMoneyForDevelopment(
                _amount,
                out int newBalance);
            _status = added
                ? $"PASS: добавлено {_amount} Money. Balance={newBalance}."
                : "FAIL: Money не добавлены.";
            _statusType = added ? MessageType.Info : MessageType.Error;
            _repaint?.Invoke();
        }
    }
}
