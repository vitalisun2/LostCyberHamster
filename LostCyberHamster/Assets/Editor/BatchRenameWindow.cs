using UnityEditor;
using UnityEngine;

public class BatchRenameWindow : EditorWindow
{
    private string findString = "01_";    // The string you want to replace
    private string replaceString = "";    // The string you want to replace it with
    private string prefix = "";           // The prefix you want to add
    private string postfix = "";          // The postfix you want to add

    [MenuItem("Tools/Batch Rename - Replace, Add Prefix, Add Postfix")]
    public static void ShowWindow()
    {
        GetWindow<BatchRenameWindow>("Batch Rename");
    }

    private void OnGUI()
    {
        GUILayout.Label("Batch Rename (Replace, Add Prefix, Add Postfix)", EditorStyles.boldLabel);

        // Input fields for replacing strings
        GUILayout.Label("Replace String in Names", EditorStyles.boldLabel);
        findString = EditorGUILayout.TextField("Find", findString);
        replaceString = EditorGUILayout.TextField("Replace With", replaceString);

        // Button for replacing strings
        if (GUILayout.Button("Replace String"))
        {
            ReplaceSelectedAssets();
        }

        GUILayout.Space(10); // Add some space between sections

        // Input field for adding a prefix
        GUILayout.Label("Add Prefix to Names", EditorStyles.boldLabel);
        prefix = EditorGUILayout.TextField("Prefix", prefix);

        // Button for adding a prefix
        if (GUILayout.Button("Add Prefix"))
        {
            AddPrefixToSelectedAssets();
        }

        GUILayout.Space(10); // Add some space between sections

        // Input field for adding a postfix
        GUILayout.Label("Add Postfix to Names", EditorStyles.boldLabel);
        postfix = EditorGUILayout.TextField("Postfix", postfix);

        // Button for adding a postfix
        if (GUILayout.Button("Add Postfix"))
        {
            AddPostfixToSelectedAssets();
        }
    }

    private void ReplaceSelectedAssets()
    {
        Object[] selectedAssets = Selection.objects;

        if (selectedAssets.Length == 0)
        {
            Debug.LogWarning("No assets selected for renaming.");
            return;
        }

        for (int i = 0; i < selectedAssets.Length; i++)
        {
            string originalName = selectedAssets[i].name;

            // Replace occurrences of findString with replaceString
            string newName = originalName.Replace(findString, replaceString);

            // Rename the asset
            AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(selectedAssets[i]), newName);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Assets renamed successfully.");
    }

    private void AddPrefixToSelectedAssets()
    {
        Object[] selectedAssets = Selection.objects;

        if (selectedAssets.Length == 0)
        {
            Debug.LogWarning("No assets selected for renaming.");
            return;
        }

        for (int i = 0; i < selectedAssets.Length; i++)
        {
            string originalName = selectedAssets[i].name;

            // Add prefix to the original name
            string newName = prefix + originalName;

            // Rename the asset
            AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(selectedAssets[i]), newName);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Prefix added to assets successfully.");
    }

    private void AddPostfixToSelectedAssets()
    {
        Object[] selectedAssets = Selection.objects;

        if (selectedAssets.Length == 0)
        {
            Debug.LogWarning("No assets selected for renaming.");
            return;
        }

        for (int i = 0; i < selectedAssets.Length; i++)
        {
            string originalName = selectedAssets[i].name;

            // Add postfix to the original name
            string newName = originalName + postfix;

            // Rename the asset
            AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(selectedAssets[i]), newName);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Postfix added to assets successfully.");
    }
}
