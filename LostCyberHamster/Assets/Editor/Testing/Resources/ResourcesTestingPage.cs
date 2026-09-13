using System;
using GameManagement;
using LostCyberHamster.Editor.Testing;
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
            using (TestingWindowLayout.BeginCenteredColumn())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(
                            "Back",
                            TestingWindowLayout.ButtonStyle,
                            GUILayout.Width(140f),
                            GUILayout.Height(60f)))
                    {
                        navigateBack?.Invoke();
                    }

                    GUILayout.Space(12f);
                    EditorGUILayout.LabelField("Resources", TestingWindowLayout.PageTitleStyle);
                }

                TestingWindowLayout.SpaceSection();
                bool isReady = EditorApplication.isPlaying && ResourceManager.IsReady;
                int previousAmount = _amount;
                using (TestingWindowLayout.BeginCard())
                {
                    EditorGUILayout.LabelField("MONEY", TestingWindowLayout.SectionTitleStyle);
                    EditorGUILayout.LabelField(
                        "Точное DEV-начисление Money. Значение и результат совпадают с runtime DEV.",
                        TestingWindowLayout.BodyStyle);
                    EditorGUILayout.Space(8f);
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
                    EditorGUILayout.Space(8f);
                    using (new EditorGUI.DisabledScope(!isReady || !amountValid))
                    {
                        if (GUILayout.Button(
                                "Add Money",
                                TestingWindowLayout.ButtonStyle,
                                GUILayout.Height(68f)))
                        {
                            AddMoney();
                        }
                    }

                    TestingWindowLayout.SpaceSection();
                    EditorGUILayout.LabelField("STATUS", TestingWindowLayout.SectionTitleStyle);
                    if (!isReady)
                    {
                        EditorGUILayout.LabelField(
                            "Resources доступны в Play Mode после загрузки PlayerData.",
                            TestingWindowLayout.BodyStyle);
                    }
                    else if (_amount <= 0)
                    {
                        EditorGUILayout.LabelField("Amount должен быть больше 0.", TestingWindowLayout.BodyStyle);
                    }
                    else if (!amountValid)
                    {
                        EditorGUILayout.LabelField("Amount переполняет Money balance.", TestingWindowLayout.BodyStyle);
                    }
                    else
                    {
                        EditorGUILayout.LabelField(_status, TestingWindowLayout.BodyStyle);
                    }
                }
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
