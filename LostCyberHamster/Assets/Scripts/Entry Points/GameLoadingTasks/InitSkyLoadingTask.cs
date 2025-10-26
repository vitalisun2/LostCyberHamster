using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Installers.Roots;
using Assets.Scripts.System;
using Assets.Scripts.System.Rendering;
using LoadingTasks;
using UnityEngine;
using Zenject;

namespace Assets.Scripts.Entry_Points.GameLoadingTasks
{
    [Serializable]
    public class InitSkyLoadingTask : ILoadingTaskSequence
    {
        public string Name => "Инициализация неба";

        [SerializeReference]
        private List<ILoadingTask> _children = new();
        public List<ILoadingTask> Children => _children;

        private EnvironmentRoot _environmentRoot;

        public async Task LoadAsync(Dictionary<string, object> bundle)
        {
            _environmentRoot = (EnvironmentRoot)bundle["environmentRoot"];

            InitSky();
        }

        private void InitSky()
        {
            var levelData = LevelController.Instance.LevelData;
            var skyPrefab = levelData.ScrollingEnvironmentPrefab;
            var skySprite = levelData.SkySprite;

            if (skyPrefab == null || skySprite == null)
            {
                Debug.LogError("[InitSkyLoadingTask] Sky prefab or sprite is missing.");
                return;
            }

            var spriteRendererOnPrefab = skyPrefab.GetComponentInChildren<SpriteRenderer>();
            var spriteWidth = skySprite.bounds.size.x;
            if (spriteWidth <= 0f && spriteRendererOnPrefab != null)
            {
                spriteWidth = spriteRendererOnPrefab.bounds.size.x;
            }

            var firstGO = GameObject.Instantiate(
                skyPrefab,
                new Vector3(0f, Consts.SkyYPos, 0f),
                Quaternion.identity,
                _environmentRoot.transform);

            var secondGO = GameObject.Instantiate(
                skyPrefab,
                new Vector3(spriteWidth, Consts.SkyYPos, 0f),
                Quaternion.identity,
                _environmentRoot.transform);

            ApplySpriteAndMaterial(firstGO, skySprite);
            ApplySpriteAndMaterial(secondGO, skySprite);

            // Set sorting layer to "Sky" for rendering behind background
            SetSortingLayer(firstGO, "Sky");
            SetSortingLayer(secondGO, "Sky");

            var firstSky = firstGO.GetComponent<ScrollingEnvironment>();
            var secondSky = secondGO.GetComponent<ScrollingEnvironment>();

            firstSky.Initialize(Consts.SkyScrollSpeed);
            secondSky.Initialize(Consts.SkyScrollSpeed);

            firstSky.RefreshScrollBounds();
            secondSky.RefreshScrollBounds();

            levelData.GameManager.AddListener(firstSky);
            levelData.GameManager.AddListener(secondSky);
        }

        private void ApplySpriteAndMaterial(GameObject target, Sprite sprite)
        {
            var renderer = target.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer == null)
            {
                Debug.LogError("[InitSkyLoadingTask] Sky prefab has no SpriteRenderer.");
                return;
            }

            SpriteRendererMaterialHelper.ApplySpriteWithDefaultMaterial(renderer, sprite);
        }

        private void SetSortingLayer(GameObject target, string layerName)
        {
            var renderer = target.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer == null)
            {
                Debug.LogWarning("[InitSkyLoadingTask] Cannot set sorting layer - no SpriteRenderer found.");
                return;
            }

            renderer.sortingLayerName = layerName;
        }

    }
}
