using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Installers.Roots;
using Assets.Scripts.System;
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

        private static Material s_SpritesDefaultMat;

        public async Task LoadAsync(Dictionary<string, object> bundle)
        {
            _environmentRoot = (EnvironmentRoot)bundle["environmentRoot"];

            InitBackgrounds();
        }

        private void InitBackgrounds()
        {
            var levelData = LevelController.Instance.LevelData;
            var backgroundPrefab = levelData.BackgroundPrefab;
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

            var firstBackground = firstGO.GetComponent<Background>();
            var secondBackground = secondGO.GetComponent<Background>();

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

            renderer.sprite = sprite;
            renderer.SetPropertyBlock(null);

            var material = GetSpritesDefaultMaterial();
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static Material GetSpritesDefaultMaterial()
        {
            if (s_SpritesDefaultMat != null)
            {
                return s_SpritesDefaultMat;
            }

            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogWarning("[InitBackgroundLoadingTask] Shader 'Sprites/Default' not found.");
                return null;
            }

            s_SpritesDefaultMat = new Material(shader);
            return s_SpritesDefaultMat;
        }

    }
}
