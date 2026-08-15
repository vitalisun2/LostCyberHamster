#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LostCyberHamster.Editor
{
    /// <summary>
    /// Показывает простой Add Skin workflow с двумя source folders.
    /// </summary>
    internal sealed class SkinAddWindow : EditorWindow
    {
        private string _skinName = string.Empty;
        private int _price;
        private string _normalSourceFolder = string.Empty;
        private string _skateboardSourceFolder = string.Empty;
        private IReadOnlyList<string> _normalExpectedSheets =
            Array.Empty<string>();
        private IReadOnlyList<string> _skateboardExpectedSheets =
            Array.Empty<string>();
        private string _schemaError;
        private Vector2 _scrollPosition;
        private bool _isBusy;

        [MenuItem("Tools/Skins/Add Skin")]
        public static void Open()
        {
            SkinAddWindow window = GetWindow<SkinAddWindow>();
            window.titleContent = new GUIContent("Add Skin");
            window.minSize = new Vector2(520f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            ReloadSchema();
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(
                _scrollPosition);
            EditorGUILayout.LabelField("Add Skin", EditorStyles.boldLabel);
            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                "Enter one name and price, then select one sprite-sheet " +
                "folder for each mode.",
                MessageType.Info);

            _skinName = EditorGUILayout.TextField(
                "Skin Name",
                _skinName);
            _price = EditorGUILayout.IntField("Price", _price);
            EditorGUILayout.Space(10f);

            DrawSourceFolder(
                "Normal Mode",
                ref _normalSourceFolder,
                _normalExpectedSheets);
            EditorGUILayout.Space(10f);
            DrawSourceFolder(
                "Skateboard Mode",
                ref _skateboardSourceFolder,
                _skateboardExpectedSheets);

            if (!string.IsNullOrWhiteSpace(_schemaError))
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.HelpBox(
                    _schemaError,
                    MessageType.Error);
                if (GUILayout.Button("Reload Default Template"))
                    ReloadSchema();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space(8f);
            EditorGUI.BeginDisabledGroup(
                _isBusy || !string.IsNullOrWhiteSpace(_schemaError));
            if (GUILayout.Button("Add Skin", GUILayout.Height(32f)))
                ExecuteAddSkin();
            EditorGUI.EndDisabledGroup();
        }

        private static void DrawSourceFolder(
            string modeName,
            ref string sourceFolder,
            IReadOnlyList<string> expectedSheets)
        {
            EditorGUILayout.LabelField(modeName, EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            sourceFolder = EditorGUILayout.TextField(
                "Source Folder",
                sourceFolder);
            if (GUILayout.Button("Browse...", GUILayout.Width(90f)))
            {
                string selected = EditorUtility.OpenFolderPanel(
                    $"Select {modeName} Sprite Sheets",
                    sourceFolder,
                    string.Empty);
                if (!string.IsNullOrWhiteSpace(selected))
                    sourceFolder = selected;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(
                "Expected animations (sprite sheets):");
            using (new EditorGUI.IndentLevelScope())
            {
                foreach (string path in expectedSheets)
                    EditorGUILayout.LabelField($"• {path}");
            }
        }

        private void ReloadSchema()
        {
            try
            {
                _normalExpectedSheets =
                    SkinAddService.GetExpectedSpriteSheets(
                        isSkateboard: false);
                _skateboardExpectedSheets =
                    SkinAddService.GetExpectedSpriteSheets(
                        isSkateboard: true);
                _schemaError = null;
            }
            catch (Exception exception)
            {
                _normalExpectedSheets = Array.Empty<string>();
                _skateboardExpectedSheets = Array.Empty<string>();
                _schemaError = exception.Message;
            }
        }

        private void ExecuteAddSkin()
        {
            _isBusy = true;
            try
            {
                var request = new SkinAddRequest(
                    _skinName,
                    _price,
                    _normalSourceFolder,
                    _skateboardSourceFolder);
                string result = SkinAddService.AddSkin(request);
                Close();
                Debug.Log("[Add Skin] " + result);
                EditorUtility.DisplayDialog(
                    "Skin Added",
                    result,
                    "OK");
            }
            catch (Exception exception)
            {
                Debug.LogError("[Add Skin] " + exception.Message);
                EditorUtility.DisplayDialog(
                    "Add Skin Failed",
                    exception.Message,
                    "OK");
            }
            finally
            {
                _isBusy = false;
            }
        }
    }
}
#endif
