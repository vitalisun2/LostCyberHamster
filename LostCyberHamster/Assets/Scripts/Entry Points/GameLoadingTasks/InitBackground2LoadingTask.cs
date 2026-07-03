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
    public class InitBackground2LoadingTask : ILoadingTaskSequence
    {
        public string Name => "Инициализация второго фона";

        [SerializeReference]
        private List<ILoadingTask> _children = new();
        public List<ILoadingTask> Children => _children;

        private EnvironmentRoot _environmentRoot;

        public async Task LoadAsync(Dictionary<string, object> bundle)
        {
            _environmentRoot = (EnvironmentRoot)bundle["environmentRoot"];

            InitBackground2();
        }

        private void InitBackground2()
        {
            var levelData = LevelController.Instance.LevelData;
            var background2Prefab = levelData.ScrollingEnvironmentPrefab;
            var background2Sprite = levelData.Background2Sprite;

            // Background2 is optional - if not configured, skip
            if (background2Sprite == null)
            {
                return;
            }

            if (background2Prefab == null)
            {
                Debug.LogError("[InitBackground2LoadingTask] Background2 prefab is missing.");
                return;
            }

            var spriteRendererOnPrefab = background2Prefab.GetComponentInChildren<SpriteRenderer>();
            var spriteWidth = background2Sprite.bounds.size.x;
            if (spriteWidth <= 0f && spriteRendererOnPrefab != null)
            {
                spriteWidth = spriteRendererOnPrefab.bounds.size.x;
            }

            var pivotY = EnvironmentLayerPlacement.GetPivotYForBottom(background2Sprite, Consts.Background2BottomYPos);

            var firstGO = GameObject.Instantiate(
                background2Prefab,
                new Vector3(0f, pivotY, 0f),
                Quaternion.identity,
                _environmentRoot.transform);

            var secondGO = GameObject.Instantiate(
                background2Prefab,
                new Vector3(spriteWidth, pivotY, 0f),
                Quaternion.identity,
                _environmentRoot.transform);

            ApplySpriteAndMaterial(firstGO, background2Sprite);
            ApplySpriteAndMaterial(secondGO, background2Sprite);

            // Set sorting layer to "Background2"
            SetSortingLayer(firstGO, "Background2");
            SetSortingLayer(secondGO, "Background2");

            var firstBackground2 = firstGO.GetComponent<ScrollingEnvironment>();
            var secondBackground2 = secondGO.GetComponent<ScrollingEnvironment>();

            firstBackground2.Initialize(Consts.Background2ScrollSpeed);
            secondBackground2.Initialize(Consts.Background2ScrollSpeed);

            firstBackground2.RefreshScrollBounds();
            secondBackground2.RefreshScrollBounds();

            levelData.GameManager.AddListener(firstBackground2);
            levelData.GameManager.AddListener(secondBackground2);
        }

        private void ApplySpriteAndMaterial(GameObject target, Sprite sprite)
        {
            var renderer = target.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer == null)
            {
                Debug.LogError("[InitBackground2LoadingTask] Background2 prefab has no SpriteRenderer.");
                return;
            }

            SpriteRendererMaterialHelper.ApplySpriteWithDefaultMaterial(renderer, sprite);
        }

        private void SetSortingLayer(GameObject target, string layerName)
        {
            var renderer = target.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer == null)
            {
                Debug.LogWarning("[InitBackground2LoadingTask] Cannot set sorting layer - no SpriteRenderer found.");
                return;
            }

            renderer.sortingLayerName = layerName;
        }

    }
}
