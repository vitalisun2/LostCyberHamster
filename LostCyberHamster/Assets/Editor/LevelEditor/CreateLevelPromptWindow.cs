using System;
using UnityEditor;
using UnityEngine;

public sealed class CreateLevelPromptWindow : EditorWindow
{
    private const float WindowWidth = 360f;
    private const float WindowHeight = 88f;

    private string _titleText;
    private string _inputLabel;
    private string _confirmButtonText;
    private string _inputValue;
    private Action<string> _onConfirm;

    public static void Show(string titleText, string inputLabel, string initialValue, string confirmButtonText, Action<string> onConfirm)
    {
        var window = CreateInstance<CreateLevelPromptWindow>();
        window.titleContent = new GUIContent(titleText);
        window._titleText = titleText;
        window._inputLabel = inputLabel;
        window._inputValue = initialValue ?? string.Empty;
        window._confirmButtonText = confirmButtonText;
        window._onConfirm = onConfirm;
        window.minSize = new Vector2(WindowWidth, WindowHeight);
        window.maxSize = new Vector2(WindowWidth, WindowHeight);
        window.position = new Rect(
            (Screen.currentResolution.width - WindowWidth) * 0.5f,
            (Screen.currentResolution.height - WindowHeight) * 0.5f,
            WindowWidth,
            WindowHeight);
        window.ShowUtility();
        window.Focus();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(_titleText, EditorStyles.boldLabel);
        GUI.SetNextControlName("create-name-field");
        _inputValue = EditorGUILayout.TextField(_inputLabel, _inputValue);

        GUILayout.Space(8f);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Cancel", GUILayout.Width(80f)))
            {
                Close();
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_inputValue)))
            {
                if (GUILayout.Button(_confirmButtonText, GUILayout.Width(80f)))
                {
                    Confirm();
                }
            }
        }

        if (Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
        {
            Confirm();
            Event.current.Use();
        }

        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
        {
            Close();
            Event.current.Use();
        }
    }

    private void OnFocus()
    {
        EditorGUI.FocusTextInControl("create-name-field");
    }

    private void Confirm()
    {
        var normalizedValue = _inputValue?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedValue))
            return;

        try
        {
            _onConfirm?.Invoke(normalizedValue);
        }
        finally
        {
            Close();
        }
    }
}
