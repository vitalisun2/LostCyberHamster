using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Installers.Roots;
using Assets.Scripts.System;
using Assets.Scripts.System.Rendering;
using LoadingTasks;
using UnityEngine;

namespace Assets.Scripts.Entry_Points.GameLoadingTasks
{
    [Serializable]
    public class InitRoadLoadingTask : ILoadingTaskSequence
    {
        public string Name => "Инициализация дороги";

        [SerializeReference]
        private List<ILoadingTask> _children = new();
        public List<ILoadingTask> Children => _children;

        private EnvironmentRoot _environmentRoot;

        public async Task LoadAsync(Dictionary<string, object> bundle)
        {
            _environmentRoot = (EnvironmentRoot)bundle["environmentRoot"];

            InitRoads();
        }

        private void InitRoads()
        {
            var levelData = LevelController.Instance.LevelData;
            var roadPrefab = levelData.RoadPrefab;
            var roadSprite = levelData.RoadSprite;

            if (roadPrefab == null || roadSprite == null)
            {
                Debug.LogError("[InitRoadLoadingTask] Road prefab or sprite is missing.");
                return;
            }

            var spriteRendererOnPrefab = roadPrefab.GetComponentInChildren<SpriteRenderer>();
            var spriteWidth = roadSprite.bounds.size.x;
            if (spriteWidth <= 0f && spriteRendererOnPrefab != null)
            {
                spriteWidth = spriteRendererOnPrefab.bounds.size.x;
            }

            var firstGO = GameObject.Instantiate(
                roadPrefab,
                new Vector3(0f, Consts.RoadYPos, 0f),
                Quaternion.identity,
                _environmentRoot.transform);

            var secondGO = GameObject.Instantiate(
                roadPrefab,
                new Vector3(spriteWidth, Consts.RoadYPos, 0f),
                Quaternion.identity,
                _environmentRoot.transform);

            ApplySpriteAndMaterial(firstGO, roadSprite);
            ApplySpriteAndMaterial(secondGO, roadSprite);

            var firstRoad = firstGO.GetComponent<ScrollingEnvironment>();
            var secondRoad = secondGO.GetComponent<ScrollingEnvironment>();

            firstRoad.RefreshScrollBounds();
            secondRoad.RefreshScrollBounds();

            levelData.GameManager.AddListener(firstRoad);
            levelData.GameManager.AddListener(secondRoad);
        }

        private void ApplySpriteAndMaterial(GameObject target, Sprite sprite)
        {
            var renderer = target.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer == null)
            {
                Debug.LogError("[InitRoadLoadingTask] Road prefab has no SpriteRenderer.");
                return;
            }

            SpriteRendererMaterialHelper.ApplySpriteWithDefaultMaterial(renderer, sprite);
        }

    }
}
