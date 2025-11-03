using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Installers.Roots;
using Assets.Scripts.System;
using Assets.Scripts.System.Rendering;
using UnityEngine;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Assets.Scripts.Entry_Points.GameLoadingTasks
{
    public static class ObstacleFactory
    {
        // Animation constants
        private const string _animatorPlayStateName = "Play";
        private const string _animationTypeIdle = "idle";
        private const string _spritePropertyName = "m_Sprite";
        
        public static List<InstantiatedObstacle> CreateObstacles(EnvironmentRoot environmentRoot)
        {
            var levelInfo = LevelController.Instance.LevelData.LevelInfo;

            List<InstantiatedObstacle> instantiatedObstacles = new();

            for (int patternIndex = 0; patternIndex < levelInfo.patterns.Count; patternIndex++)
            {
                foreach (var model in levelInfo.patterns[patternIndex].obstacles)
                {
                    var spriteName = Path.GetFileNameWithoutExtension(model.spriteName);
                    var spawnPosition = new Vector3(model.x, model.y, 0);
                    var obstacleScript = CreateObstacle(model, spriteName, spawnPosition, environmentRoot);
                    LevelController.Instance.LevelData.GameManager.AddListener(obstacleScript);

                    instantiatedObstacles.Add(new InstantiatedObstacle(obstacleScript, spriteName, spawnPosition, patternIndex));
                }
            }

            return instantiatedObstacles;
        }

        private static Obstacle CreateObstacle(ObstacleModel model, string spriteName, Vector3 spawnPosition,
            EnvironmentRoot environmentRoot)
        {
            var prefab = GetPrefab(model.type);

            var obstacleInst = Object.Instantiate(prefab, spawnPosition, Quaternion.identity, environmentRoot.ObstaclesPool);

            ShiftUpYPosForBonuses(obstacleInst, model.type);

            var renderer = obstacleInst.GetComponentInChildren<SpriteRenderer>();
            var animator = obstacleInst.GetComponentInChildren<Animator>();
            
            // Try to setup animation first, fallback to static sprite if no animation
            var hasAnimation = TrySetupAnimation(spriteName, renderer, animator, model.type);
            
            // If no animation, load and validate static sprite
            if (!hasAnimation)
            {
                var rendererSprite = GetRendererSpriteByModelTypeAndName(model.type, spriteName);
                SetStaticSprite(renderer, rendererSprite, animator);
            }
            
            renderer.sortingLayerName = GetSortingLayer(model.y);

            var boxCollider = renderer.GetComponentInChildren<BoxCollider2D>();
            
            // Set collider bounds from sprite
            if (renderer.sprite != null)
            {
                boxCollider.size = renderer.sprite.bounds.size;
                boxCollider.offset = renderer.sprite.bounds.center;
            }
            else
            {
                Debug.LogError($"[ObstacleFactory] Sprite is null for obstacle '{spriteName}' after setup!");
            }

            var obstacleScript = obstacleInst.AddComponent<Obstacle>();

            obstacleScript.Init((ObstacleTypeEnum)model.type, LevelController.Instance.LevelData.GameManager, spriteName);

            return obstacleScript;
        }

        private static Sprite GetRendererSpriteByModelTypeAndName(int modelType, string spriteName)
        {
            Sprite sprite = null;

            if (new List<ObstacleTypeEnum>
                {
                    ObstacleTypeEnum.smallAlive,
                    ObstacleTypeEnum.smallNotAliveRoad,
                    ObstacleTypeEnum.smallNotAliveRoadAndRoof,
                    ObstacleTypeEnum.bigAlive,
                    ObstacleTypeEnum.bigNotAlive
                }.Contains((ObstacleTypeEnum)modelType))
                sprite = LevelController.Instance.LevelData.ObstaclesSprites.FirstOrDefault(s =>
                    string.Equals(s.name, spriteName, StringComparison.OrdinalIgnoreCase));

            var collectableTypes = new List<ObstacleTypeEnum>
            {
                ObstacleTypeEnum.collectableEnergetic,
                ObstacleTypeEnum.collectablePizza,
                ObstacleTypeEnum.collectableCrystal,
                ObstacleTypeEnum.collectableLife,
                ObstacleTypeEnum.collectableCoin
            };

            if (collectableTypes.Contains((ObstacleTypeEnum)modelType))
                sprite = LevelController.Instance.LevelData.CollectablesSprites.FirstOrDefault(s =>
                    string.Equals(s.name, spriteName, StringComparison.OrdinalIgnoreCase));

            if (ObstacleTypeEnum.decor == (ObstacleTypeEnum)modelType)
                sprite = LevelController.Instance.LevelData.DecorSprites.FirstOrDefault(s =>
                    string.Equals(s.name, spriteName, StringComparison.OrdinalIgnoreCase));

            LevelDataValidator.ValidateObstacleSprite((ObstacleTypeEnum)modelType, spriteName, sprite);

            return sprite;
        }

        private static GameObject GetPrefab(int type)
        {
            var levelData = LevelController.Instance.LevelData;

            List<ObstacleTypeEnum> smallObstacleTypes = new List<ObstacleTypeEnum>
            {
                ObstacleTypeEnum.smallAlive,
                ObstacleTypeEnum.smallNotAliveRoad,
                ObstacleTypeEnum.smallNotAliveRoadAndRoof,
                ObstacleTypeEnum.collectableEnergetic,
                ObstacleTypeEnum.collectablePizza,
                ObstacleTypeEnum.collectableCrystal,
                ObstacleTypeEnum.collectableLife,
                ObstacleTypeEnum.collectableCoin
            };

            if (smallObstacleTypes.Contains((ObstacleTypeEnum)type))
                return levelData.SmallCitizenPrefab;

            if (type == (int)ObstacleTypeEnum.bigAlive)
                return levelData.BigCitizenPrefab;

            return levelData.BigNotAlivePrefab;
        }

        private static string GetSortingLayer(float yPosition)
        {
            return Math.Abs(yPosition - Consts.ObstacleY0Pos) < 0.01f ? "UpperSprites" : "LowerSprites";
        }

        private static void ShiftUpYPosForBonuses(GameObject obstacleInst, int modelType)
        {
            var firstChild = obstacleInst.transform.GetChild(0).gameObject;

            if (new List<ObstacleTypeEnum>()
                {
                    ObstacleTypeEnum.collectableEnergetic,
                    ObstacleTypeEnum.collectablePizza,
                    ObstacleTypeEnum.collectableCrystal,
                    ObstacleTypeEnum.collectableLife,
                    ObstacleTypeEnum.collectableCoin
                }.Contains((ObstacleTypeEnum)modelType))
            {
                firstChild.transform.position += new Vector3(0, Consts.BonusYOffset, 0);
            }
        }

        /// <summary>
        /// Attempts to setup animation for an obstacle using AnimatorOverrideController.
        /// Uses naming convention: {spriteName}_{animationType} (e.g., "obstacle_new_york_dog_idle").
        /// </summary>
        /// <param name="spriteName">Base name of the obstacle sprite</param>
        /// <param name="renderer">SpriteRenderer component to apply the first frame to</param>
        /// <param name="animator">Animator component to configure</param>
        /// <param name="modelType">Type of obstacle for validation</param>
        /// <returns>True if animation was found and configured, false otherwise</returns>
        private static bool TrySetupAnimation(string spriteName, SpriteRenderer renderer, Animator animator, int modelType)
        {
            var animationClip = TryGetAnimationClip(spriteName, _animationTypeIdle);
            
            if (animationClip != null && animator != null)
            {
                // Has animation - use AnimatorOverrideController to replace the clip
                var overrideController = new AnimatorOverrideController(animator.runtimeAnimatorController);
                overrideController[_animatorPlayStateName] = animationClip;
                animator.runtimeAnimatorController = overrideController;
                
                // Validate that animation has at least one frame with sprite
                ValidateAnimationClip(animationClip, spriteName, (ObstacleTypeEnum)modelType);
                
                // Manually set first sprite from animation clip
                SetFirstSpriteFromAnimation(renderer, animationClip, spriteName);
                
                return true;
            }
            
            return false;
        }
        
        private static void SetFirstSpriteFromAnimation(SpriteRenderer renderer, AnimationClip clip, string spriteName)
        {
#if UNITY_EDITOR
            // Get first sprite frame from animation clip
            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            var spriteBinding = bindings.FirstOrDefault(b => b.type == typeof(SpriteRenderer) && b.propertyName == _spritePropertyName);
            
            if (spriteBinding.path != null)
            {
                var keyframes = AnimationUtility.GetObjectReferenceCurve(clip, spriteBinding);
                if (keyframes != null && keyframes.Length > 0)
                {
                    var firstSprite = keyframes[0].value as Sprite;
                    if (firstSprite != null)
                    {
                        SpriteRendererMaterialHelper.ApplySpriteWithDefaultMaterial(renderer, firstSprite);
                        return;
                    }
                }
            }
            
            Debug.LogError($"[ObstacleFactory] Failed to extract first sprite from animation clip '{clip.name}' for obstacle '{spriteName}'. Sprite will appear after first Animator update. This should not happen in Editor mode.");
#else
            Debug.LogWarning($"[ObstacleFactory] Runtime mode: First sprite extraction skipped for '{spriteName}'. Animation will start on first Animator update.");
#endif
        }
        
        /// <summary>
        /// Validates that an AnimationClip contains valid sprite frames with correct dimensions.
        /// Checks each keyframe for null sprites and validates dimensions against obstacle type requirements.
        /// Only runs in Editor mode.
        /// </summary>
        /// <param name="clip">AnimationClip to validate</param>
        /// <param name="spriteName">Name of the obstacle for error messages</param>
        /// <param name="obstacleType">Type of obstacle to determine expected dimensions</param>
        private static void ValidateAnimationClip(AnimationClip clip, string spriteName, ObstacleTypeEnum obstacleType)
        {
#if UNITY_EDITOR
            // Check if animation clip has sprite frames
            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            var spriteBinding = bindings.FirstOrDefault(b => b.type == typeof(SpriteRenderer) && b.propertyName == _spritePropertyName);
            
            if (spriteBinding.path == null)
            {
                HelpMethods.LogAndStopGame($"[ObstacleFactory] Animation clip '{clip.name}' для препятствия '{spriteName}' (type: {obstacleType}) не содержит sprite frames!");
                return;
            }
            
            // Get all keyframes with sprites
            var keyframes = AnimationUtility.GetObjectReferenceCurve(clip, spriteBinding);
            if (keyframes == null || keyframes.Length == 0)
            {
                HelpMethods.LogAndStopGame($"[ObstacleFactory] Animation clip '{clip.name}' не содержит keyframes!");
                return;
            }
            
            // Validate each sprite frame - check individual sprite rect, not entire texture
            for (int i = 0; i < keyframes.Length; i++)
            {
                var sprite = keyframes[i].value as Sprite;
                if (sprite == null)
                {
                    HelpMethods.LogAndStopGame($"[ObstacleFactory] Animation clip '{clip.name}' содержит null sprite на кадре {i} (time: {keyframes[i].time})!");
                    continue;
                }
                
                // Validate individual sprite dimensions (not the entire texture)
                ValidateAnimationSprite(sprite, obstacleType, i);
            }
#endif
        }
        
        /// <summary>
        /// Validates dimensions of an individual sprite frame from an animation.
        /// Uses sprite.rect dimensions (not sprite.texture) to check individual frame size within sprite sheet.
        /// </summary>
        /// <param name="sprite">Sprite frame to validate</param>
        /// <param name="obstacleType">Type of obstacle to determine expected dimensions</param>
        /// <param name="frameIndex">Index of the frame in animation for error reporting</param>
        private static void ValidateAnimationSprite(Sprite sprite, ObstacleTypeEnum obstacleType, int frameIndex)
        {
            void CheckSize(int requiredWidth, int requiredHeight, string typeName)
            {
                // Use sprite.rect for individual frame size, not sprite.texture for entire sheet
                int spriteWidth = (int)sprite.rect.width;
                int spriteHeight = (int)sprite.rect.height;
                
                if (spriteWidth != requiredWidth || spriteHeight != requiredHeight)
                {
                    HelpMethods.LogAndStopGame(
                        $"[ObstacleFactory] Animation sprite '{sprite.name}' (frame {frameIndex}) " +
                        $"has resolution {spriteWidth}x{spriteHeight}, " +
                        $"expected {requiredWidth}x{requiredHeight} ({typeName}).\n" +
                        $"Note: Sprite is part of texture '{sprite.texture.name}' ({sprite.texture.width}x{sprite.texture.height})."
                    );
                }
            }

            switch (obstacleType)
            {
                case ObstacleTypeEnum.bigNotAlive:
                    CheckSize(Consts.BIG_NOTALIVE_WIDTH, Consts.BIG_NOTALIVE_HEIGHT, "bigNotAlive");
                    break;

                case ObstacleTypeEnum.bigAlive:
                    CheckSize(Consts.BIG_ALIVE_WIDTH, Consts.BIG_ALIVE_HEIGHT, "bigAlive");
                    break;

                case ObstacleTypeEnum.smallAlive:
                    CheckSize(Consts.SMALL_ALIVE_WIDTH, Consts.SMALL_ALIVE_HEIGHT, "smallAlive");
                    break;

                case ObstacleTypeEnum.smallNotAliveRoad:
                    CheckSize(Consts.SMALL_NOTALIVE_WIDTH, Consts.SMALL_NOTALIVE_HEIGHT, "smallNotAliveRoad");
                    break;

                case ObstacleTypeEnum.smallNotAliveRoadAndRoof:
                    CheckSize(Consts.SMALL_NOTALIVE_WIDTH, Consts.SMALL_NOTALIVE_HEIGHT, "smallNotAliveRoadAndRoof");
                    break;
            }
        }

        private static AnimationClip TryGetAnimationClip(string spriteName, string animationType)
        {
            var animationClipName = $"{spriteName}_{animationType}";
            var clips = LevelController.Instance.LevelData.ObstacleAnimationClips;
            
            if (clips == null || clips.Count == 0)
            {
                return null;
            }
            
            return clips.FirstOrDefault(a => string.Equals(a.name, animationClipName, StringComparison.OrdinalIgnoreCase));
        }

        private static void SetStaticSprite(SpriteRenderer renderer, Sprite sprite, Animator animator)
        {
            // Disable animator if present (for non-animated obstacles)
            if (animator != null)
            {
                animator.enabled = false;
            }
            
            // Set static sprite
            SpriteRendererMaterialHelper.ApplySpriteWithDefaultMaterial(renderer, sprite);
        }
    }
}
