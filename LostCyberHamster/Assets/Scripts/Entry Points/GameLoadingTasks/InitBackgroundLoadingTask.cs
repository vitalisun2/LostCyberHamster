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
    public class InitBackgroundLoadingTask : ILoadingTaskSequence
    {
        public string Name => "Инициализация фона";

        [SerializeReference]
        private List<ILoadingTask> _children = new();
        public List<ILoadingTask> Children => _children;

        private EnvironmentRoot _environmentRoot;

        public async Task LoadAsync(Dictionary<string, object> bundle)
        {
            _environmentRoot = (EnvironmentRoot)bundle["environmentRoot"];

            InitBackgrounds();
        }

        private void InitBackgrounds()
        {
            var levelData = LevelController.Instance.LevelData;
            var backgroundPrefab = levelData.ScrollingEnvironmentPrefab;
            var backgroundSprite = levelData.BackgroundSprite;

            if (backgroundPrefab == null || backgroundSprite == null)
            {
                Debug.LogError("[InitBackgroundLoadingTask] Background prefab or sprite is missing.");
                return;
            }

            var spriteRendererOnPrefab = backgroundPrefab.GetComponentInChildren<SpriteRenderer>();
            var spriteWidth = backgroundSprite.bounds.size.x;
            if (spriteWidth <= 0f && spriteRendererOnPrefab != null)
            {
                spriteWidth = spriteRendererOnPrefab.bounds.size.x;
            }

            var firstGO = GameObject.Instantiate(
                backgroundPrefab,
                new Vector3(0f, Consts.BackgroundYPos, 0f),
                Quaternion.identity,
                _environmentRoot.transform);

            var secondGO = GameObject.Instantiate(
                backgroundPrefab,
                new Vector3(spriteWidth, Consts.BackgroundYPos, 0f),
                Quaternion.identity,
                _environmentRoot.transform);

            ApplySpriteAndMaterial(firstGO, backgroundSprite);
            ApplySpriteAndMaterial(secondGO, backgroundSprite);

            var firstBackground = firstGO.GetComponent<ScrollingEnvironment>();
            var secondBackground = secondGO.GetComponent<ScrollingEnvironment>();

            firstBackground.Initialize(Consts.BackgroundScrollSpeed);
            secondBackground.Initialize(Consts.BackgroundScrollSpeed);

            firstBackground.RefreshScrollBounds();
            secondBackground.RefreshScrollBounds();

            levelData.GameManager.AddListener(firstBackground);
            levelData.GameManager.AddListener(secondBackground);
        }

        private void ApplySpriteAndMaterial(GameObject target, Sprite sprite)
        {
            var renderer = target.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer == null)
            {
                Debug.LogError("[InitBackgroundLoadingTask] Background prefab has no SpriteRenderer.");
                return;
            }

            SpriteRendererMaterialHelper.ApplySpriteWithDefaultMaterial(renderer, sprite);
        }

    }
}
