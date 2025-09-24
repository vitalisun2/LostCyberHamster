#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;
using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;

namespace Assets.Tests.EditMode
{
    public class LevelAddressablesTests
    {
        private const string TestGroupName = "LevelKeyTests";
        private const string TestAssetPath = "Assets/Tests/EditMode/TestLevel.json";

        private AddressableAssetSettings _settings;
        private AddressableAssetGroup _group;
        private AddressableAssetEntry _entry;
        private LevelKey _levelKey;

        [SetUp]
        public void SetUp()
        {
            _settings = AddressableAssetSettingsDefaultObject.Settings;
            Assert.IsNotNull(_settings, "Addressable settings are missing.");

            _group = _settings.groups.FirstOrDefault(g => g != null && g.Name == TestGroupName);
            if (_group == null)
            {
                _group = _settings.CreateGroup(TestGroupName, false, false, false, new[] { typeof(BundledAssetGroupSchema) });
            }

            EnsureTestAsset();
            var guid = AssetDatabase.AssetPathToGUID(TestAssetPath);
            _entry = _settings.CreateOrMoveEntry(guid, _group, false, false);

            _levelKey = new LevelKey("test_location", PartOfDay.Morning, 99);
            _entry.address = LevelPathBuilder.Build(_levelKey);
            _entry.SetLabel($"Location_{_levelKey.LocationId}", true, true);
            _entry.SetLabel($"PartOfDay_{PartOfDay.Morning}", true, true);

            _settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, _entry, true);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureTestAsset()
        {
            var directory = Path.GetDirectoryName(TestAssetPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(TestAssetPath))
            {
                var asset = new TextAsset("test level data");
                AssetDatabase.CreateAsset(asset, TestAssetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(TestAssetPath, ImportAssetOptions.ForceSynchronousImport);
            }
            else
            {
                AssetDatabase.ImportAsset(TestAssetPath, ImportAssetOptions.ForceSynchronousImport);
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (_entry != null)
            {
                _settings.RemoveAssetEntry(_entry.guid);
            }

            if (File.Exists(TestAssetPath))
            {
                AssetDatabase.DeleteAsset(TestAssetPath);
            }

            if (_group != null && _group != _settings.DefaultGroup && _group.entries.Count == 0)
            {
                var groupPath = AssetDatabase.GetAssetPath(_group);
                _settings.RemoveGroup(_group);
                if (!string.IsNullOrEmpty(groupPath))
                {
                    AssetDatabase.DeleteAsset(groupPath);
                }
            }

            AssetDatabase.SaveAssets();
        }

        [UnityTest]
        public IEnumerator LoadLevelJson_ByKey_Succeeds()
        {
            var handle = Addressables.LoadAssetAsync<TextAsset>(LevelPathBuilder.Build(_levelKey));

            try
            {
                yield return handle;

                Assert.AreEqual(AsyncOperationStatus.Succeeded, handle.Status);
                Assert.IsNotNull(handle.Result);
            }
            finally
            {
                Addressables.Release(handle);
            }
        }
    }
}
#endif
