using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

public class EditorHelpMethods : MonoBehaviour
{
    [MenuItem("Tools/Check Missing References in Prefabs")]
    private static void CheckMissingReferencesInPrefabs()
    {
        var allPrefabs = GetAllPrefabs();
        foreach (var prefab in allPrefabs)
        {
            var gameObject = AssetDatabase.LoadAssetAtPath(prefab, typeof(GameObject)) as GameObject;
            var components = gameObject.GetComponentsInChildren<Component>();

            foreach (var component in components)
            {
                if (!component)
                {
                    Debug.LogError("Missing component in prefab: " + prefab, gameObject);
                    continue;
                }

                SerializedObject so = new SerializedObject(component);
                var sp = so.GetIterator();

                while (sp.NextVisible(true))
                {
                    if (sp.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        if (sp.objectReferenceValue == null
                            && sp.objectReferenceInstanceIDValue != 0)
                        {
                            ShowError(component, prefab, sp);
                        }
                    }
                }

                if (component is MonoBehaviour monoBehaviour)
                {
                    foreach (var field in monoBehaviour.GetType().GetFields( BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (field.FieldType.IsSubclassOf(typeof(UnityEngine.Object)))
                        {
                            var value = field.GetValue(monoBehaviour) as UnityEngine.Object;
                            if (value == null)
                            {
                                Debug.LogError($"Missing reference in MonoBehaviour: {monoBehaviour.GetType().Name}, field: {field.Name}", gameObject);
                            }
                        }
                    }
                }

                // Check for missing references in Button's onClick events
                if (component is Button button)
                {
                    for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                    {
                        if (button.onClick.GetPersistentTarget(i) == null ||
                            string.IsNullOrEmpty(button.onClick.GetPersistentMethodName(i)))
                        {
                            Debug.LogError($"Missing onClick reference found in: {gameObject.name}, button: {button.name}", gameObject);
                        }
                    }
                }
            }
        }
    }

    [MenuItem("Tools/Update prefabs in the scene")]
    public static void UpdatePrefabs()
    {
        //todo: ���� �� ��������, ����������������.
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);

        foreach (GameObject obj in allObjects)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(obj))
            {
                PrefabUtility.RevertPrefabInstance(obj, InteractionMode.AutomatedAction);
            }
        }
    }

    [MenuItem("Tools/Check OnDestroy in Scripts")]
    public static void CheckOnDestroy()
    {
        var scriptGuids = AssetDatabase.FindAssets("t:Script", new[] { "Assets/Scripts" });
        List<string> scriptsMissingOnDestroy = new List<string>();

        foreach (var scriptGuid in scriptGuids)
        {
            var scriptPath = AssetDatabase.GUIDToAssetPath(scriptGuid);
            if (!File.Exists(scriptPath)) continue;

            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            System.Type scriptType = script.GetClass();

            if (scriptType == null || !typeof(MonoBehaviour).IsAssignableFrom(scriptType)) continue;

            var onDestroyMethod = scriptType.GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            string scriptText = File.ReadAllText(scriptPath);

            if (scriptText.Contains("+=") && 
                (scriptText.Contains("Action")|| scriptText.Contains("UnityEvent") || scriptText.Contains("GameState")) && 
                onDestroyMethod == null)
            {
                scriptsMissingOnDestroy.Add(scriptPath);
            }
        }

        if (scriptsMissingOnDestroy.Count > 0)
        {
            foreach (var scriptPath in scriptsMissingOnDestroy)
            {
                Debug.Log(scriptPath);
            }
        }
        else
        {
            Debug.Log("No scripts with event subscription missing OnDestroy method found.");
        }
    }

    private static void ShowError(Component component, string prefab, SerializedProperty sp)
    {
        var asset = AssetDatabase.LoadMainAssetAtPath(prefab);
        Debug.LogError("Missing reference found in: " + asset.name + ", object: " + component.name + ", property: " + sp.name, asset);
    }

    private static IEnumerable<string> GetAllPrefabs()
    {
        string[] allAssets = AssetDatabase.GetAllAssetPaths();
        foreach (var asset in allAssets)
        {
            if (asset.Contains(".prefab"))
                yield return asset;
        }
    }
    /*
    [MenuItem("Tools/Generate State Machine")]
    private static void GenerateStateMachine()
    {
        var typestate = typeof(IState);

        var types = AppDomain.CurrentDomain.GetAssemblies()
                                           .SelectMany(s => s.GetTypes())
                                           .Where(p => typestate.IsAssignableFrom(p) && !p.Equals(typestate))
                                           .Select(x=> x.Name)
                                           .ToList();

        string path = Path.Combine(Application.dataPath, "Scripts", "StateMachine", "GameStatesEnum.cs");
        using (StreamWriter sw = File.CreateText(path))
        {
            sw.WriteLine("public enum GameStatesEnum");
            sw.WriteLine("{");
            sw.WriteLine("\t"+string.Join(",\n\t", types));
            sw.WriteLine("}");
        }

        Debug.Log("Enum generated");

        for (int i = 0; i < types.Count; i++)
        {
            types[i] = $"new {types[i]}()";
        }


        var code = @"
using UnityEngine;
using Vues.System;

public static class GameStateMachine
{
	private static IState[] _states =
	{
        !##!
    };

    public static IState CurrentState { get; private set; }

    public static async Awaitable Transition(GameStatesEnum index)
    {
        CurrentState = _states[(int)index];
        await CurrentState.ExecuteAsync();
    }
}";



        path = Path.Combine(Application.dataPath, "Scripts", "StateMachine", "GameStateMachine.cs");

        using (StreamWriter sw = File.CreateText(path))
        {
            sw.Write(code.Replace("!##!", string.Join(",\n\t\t", types)));
        }



        Debug.Log("StateMachine generated");
    }
    */
}
