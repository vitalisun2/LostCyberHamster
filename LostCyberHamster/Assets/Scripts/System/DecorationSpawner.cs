using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Common.Models;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Installers.Roots;
using Assets.Scripts.System.Rendering;
using UnityEngine;

namespace Assets.Scripts.System
{
    /// <summary>
    /// Spawns and manages decoration objects (bushes, trees, etc.) during gameplay.
    /// Decorations are purely visual — no colliders, no gameplay interaction.
    /// They scroll with the road and are activated/deactivated based on screen visibility.
    /// </summary>
    public class DecorationSpawner : MonoBehaviour,
        Listeners.IGameStartListener,
        Listeners.IGameUpdateListener,
        Listeners.IGamePauseListener,
        Listeners.IGameResumeListener,
        Listeners.IGameFinishListener
    {
        public static DecorationSpawner Instance { get; private set; }

        private const string DecorSortingLayer = "Decor";
        private const float ActivationPadding = 3f;
        private const float DeactivationPadding = 2f;

        private List<DecorationInstance> _decorations = new();
        private Transform _container;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void Init(EnvironmentRoot environmentRoot)
        {
            _container = environmentRoot.DecorationsContainer != null
                ? environmentRoot.DecorationsContainer
                : environmentRoot.transform;

            LevelController.Instance.LevelData.GameManager.AddListener(this);

            CreateDecorations();
        }

        public void OnStart()
        {
            enabled = true;
        }

        public void OnUpdate(float deltaTime)
        {
            float screenLeft = Camera.main.transform.position.x - Camera.main.orthographicSize * Camera.main.aspect;
            float screenRight = Camera.main.transform.position.x + Camera.main.orthographicSize * Camera.main.aspect;

            for (int i = 0; i < _decorations.Count; i++)
            {
                var decor = _decorations[i];

                if (!decor.IsActive)
                {
                    // Activate when approaching right edge of screen
                    if (decor.GameObject.transform.position.x <= screenRight + ActivationPadding &&
                        decor.GameObject.transform.position.x >= screenLeft - DeactivationPadding)
                    {
                        decor.GameObject.SetActive(true);
                        decor.IsActive = true;
                        decor.ScrollMechanics = new ScrollLeftMechanics(decor.GameObject.transform, Consts.RoadScrollSpeed);
                    }

                    continue;
                }

                // Scroll
                decor.ScrollMechanics.Update(deltaTime);

                // Deactivate when past left edge
                if (decor.GameObject.transform.position.x < screenLeft - DeactivationPadding)
                {
                    decor.GameObject.SetActive(false);
                    decor.IsActive = false;
                    decor.ScrollMechanics = null;
                }
            }
        }

        public void OnPause()
        {
            enabled = false;
        }

        public void OnResume()
        {
            enabled = true;
        }

        public void OnFinish()
        {
            enabled = false;
        }

        private void CreateDecorations()
        {
            var levelInfo = LevelController.Instance.LevelData.LevelInfo;
            var decorSprites = LevelController.Instance.LevelData.DecorSprites;

            if (levelInfo.decorationPatterns == null || levelInfo.decorationPatterns.Count == 0)
            {
                return;
            }

            if (decorSprites == null || decorSprites.Count == 0)
            {
                Debug.LogWarning("[DecorationSpawner] No decor sprites loaded. Decorations will not be displayed.");
                return;
            }

            var spriteLookup = new Dictionary<string, Sprite>(global::System.StringComparer.OrdinalIgnoreCase);
            foreach (var sprite in decorSprites)
            {
                spriteLookup[sprite.name] = sprite;
            }

            foreach (var pattern in levelInfo.decorationPatterns)
            {
                if (pattern.decorationTiles == null)
                    continue;

                foreach (var tile in pattern.decorationTiles)
                {
                    if (!spriteLookup.TryGetValue(tile.name, out var sprite))
                    {
                        Debug.LogWarning($"[DecorationSpawner] Sprite '{tile.name}' not found in decor sprites.");
                        continue;
                    }

                    float worldX = tile.xPos * Consts.GridSnapStep;
                    float worldY = tile.yPos * Consts.GridSnapStep;

                    var go = CreateDecorationObject(tile.name, sprite, worldX, worldY);
                    go.SetActive(false);

                    _decorations.Add(new DecorationInstance
                    {
                        GameObject = go,
                        IsActive = false,
                        ScrollMechanics = null
                    });
                }
            }
        }

        private GameObject CreateDecorationObject(string name, Sprite sprite, float worldX, float worldY)
        {
            var go = new GameObject($"Decor_{name}");
            go.transform.SetParent(_container);
            go.transform.position = new Vector3(worldX, worldY, 0f);

            var renderer = go.AddComponent<SpriteRenderer>();
            SpriteRendererMaterialHelper.ApplySpriteWithDefaultMaterial(renderer, sprite);
            renderer.sortingLayerName = DecorSortingLayer;
            renderer.sortingOrder = Mathf.RoundToInt(-worldY * 100);

            return go;
        }

        private class DecorationInstance
        {
            public GameObject GameObject;
            public bool IsActive;
            public ScrollLeftMechanics ScrollMechanics;
        }
    }
}
