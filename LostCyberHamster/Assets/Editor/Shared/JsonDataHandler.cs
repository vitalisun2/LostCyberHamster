using System;
using System.IO;
using Assets.Scripts.Common.Models;
using UnityEngine;

namespace Assets.Editor.Shared
{
    internal class JsonDataHandler
    {
        public static LevelInfo LoadLevelInfoFromFile(string filePath)
        {
            LevelInfo result;

            try
            {
                var json = Resources.Load<TextAsset>(filePath).text;
                result = JsonUtility.FromJson<LevelInfo>(json);

                Debug.Log($"level info loaded from file at path: {filePath}");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }


            return result;
        }

        public static void SaveLevelInfoToFile(LevelInfo levelInfo, string filePath)
        {
            var json = JsonUtility.ToJson(levelInfo);

            try
            {
                File.WriteAllText(filePath, json);
                UnityEditor.AssetDatabase.Refresh();

                Debug.Log($"level info saved to file at path: {filePath}");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw;
            }
        }
    }
}
