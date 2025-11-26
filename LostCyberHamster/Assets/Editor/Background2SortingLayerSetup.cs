#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Assets.EditorTools
{
    /// <summary>
    /// Automatically creates "Background2" sorting layer if it doesn't exist.
    /// </summary>
    public static class Background2SortingLayerSetup
    {
        [MenuItem("Tools/Backgrounds/Setup Background2 Sorting Layer", priority = 101)]
        public static void SetupBackground2SortingLayer()
        {
            // Get current sorting layers
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var sortingLayersProp = tagManager.FindProperty("m_SortingLayers");

            if (sortingLayersProp == null || !sortingLayersProp.isArray)
            {
                Debug.LogError("[Background2SortingLayer] Cannot access sorting layers property.");
                return;
            }

            // Check if Background2 already exists
            for (int i = 0; i < sortingLayersProp.arraySize; i++)
            {
                var layerProp = sortingLayersProp.GetArrayElementAtIndex(i);
                var nameProp = layerProp.FindPropertyRelative("name");
                if (nameProp != null && nameProp.stringValue == "Background2")
                {
                    Debug.Log("[Background2SortingLayer] 'Background2' sorting layer already exists.");
                    return;
                }
            }

            // Find indices of Sky and Background layers
            int skyIndex = -1;
            int backgroundIndex = -1;

            for (int i = 0; i < sortingLayersProp.arraySize; i++)
            {
                var layerProp = sortingLayersProp.GetArrayElementAtIndex(i);
                var nameProp = layerProp.FindPropertyRelative("name");
                if (nameProp != null)
                {
                    if (nameProp.stringValue == "Sky")
                    {
                        skyIndex = i;
                    }
                    else if (nameProp.stringValue == "Background")
                    {
                        backgroundIndex = i;
                    }
                }
            }

            if (skyIndex == -1 || backgroundIndex == -1)
            {
                Debug.LogWarning("[Background2SortingLayer] 'Sky' or 'Background' layer not found. Adding 'Background2' at the end.");
            }

            // Determine insert position (between Sky and Background)
            int insertIndex = backgroundIndex > skyIndex && skyIndex >= 0 ? backgroundIndex : sortingLayersProp.arraySize;

            // Insert new layer
            sortingLayersProp.InsertArrayElementAtIndex(insertIndex);
            var newLayerProp = sortingLayersProp.GetArrayElementAtIndex(insertIndex);
            
            // Set layer properties
            var newNameProp = newLayerProp.FindPropertyRelative("name");
            if (newNameProp != null)
            {
                newNameProp.stringValue = "Background2";
            }

            var uniqueIDProp = newLayerProp.FindPropertyRelative("uniqueID");
            if (uniqueIDProp != null)
            {
                // Generate unique ID (Unity uses random positive integers)
                uniqueIDProp.intValue = UnityEngine.Random.Range(1000000, int.MaxValue);
            }

            tagManager.ApplyModifiedProperties();
            
            Debug.Log($"[Background2SortingLayer] Created 'Background2' sorting layer at index {insertIndex}.");
            Debug.Log("[Background2SortingLayer] Recommended order: Sky → Background2 → Background → Road");
        }

        [MenuItem("Tools/Backgrounds/List Sorting Layers", priority = 102)]
        public static void ListSortingLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var sortingLayersProp = tagManager.FindProperty("m_SortingLayers");

            if (sortingLayersProp == null || !sortingLayersProp.isArray)
            {
                Debug.LogError("[Background2SortingLayer] Cannot access sorting layers.");
                return;
            }

            Debug.Log("=== Current Sorting Layers ===");
            for (int i = 0; i < sortingLayersProp.arraySize; i++)
            {
                var layerProp = sortingLayersProp.GetArrayElementAtIndex(i);
                var nameProp = layerProp.FindPropertyRelative("name");
                var idProp = layerProp.FindPropertyRelative("uniqueID");
                
                if (nameProp != null)
                {
                    Debug.Log($"[{i}] {nameProp.stringValue} (ID: {idProp?.intValue})");
                }
            }
        }
    }
}
#endif
