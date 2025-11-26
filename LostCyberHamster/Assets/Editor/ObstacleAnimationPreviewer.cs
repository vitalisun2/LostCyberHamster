#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Assets.EditorTools
{
    /// <summary>
    /// Quick preview tool for obstacle animations.
    /// 
    /// Usage:
    /// 1. Select a sprite sheet in Project window (e.g., obstacle_new_york_people_1_idle.png)
    /// 2. Click: Tools → Obstacle Animations → Preview Selected Animation
    /// 3. Prefab spawns in scene, animation assigned, Animation window auto-plays
    /// 
    /// Supports multiple selection - creates grid layout with max 5 objects per row.
    /// 
    /// Saves time by automating:
    /// - Placing correct prefab (BigCitizen/SmallCitizen/BigNotAlive) in scene
    /// - Assigning animation to Animator via AnimatorOverrideController
    /// - Opening Animation window and starting playback
    /// </summary>
    public static class ObstacleAnimationPreviewer
    {
        private const string BigCitizenPrefabPath = "Assets/Content/prefabs/BigCitizenPrefab.prefab";
        private const string SmallCitizenPrefabPath = "Assets/Content/prefabs/SmallCitizenPrefab.prefab";
        private const string BigNotAlivePrefabPath = "Assets/Content/prefabs/BigNotAlivePrefab.prefab";
        
        private const string AnimatorPlaceholderClipName = "EmptyClip";
        private const int MaxObjectsPerRow = 5;
        private const float SpacingMultiplier = 1.2f; // 20% spacing between objects
        private const string AnimatorStateName = "Play"; // Default state name in ObstacleAnimatorController
        
        // Store current preview instances to cleanup on next preview
        private static List<GameObject> currentPreviewInstances = new List<GameObject>();
        private static List<Animator> currentAnimators = new List<Animator>();
        private static List<GameObject> currentAnimatedObjects = new List<GameObject>();
        private static List<AnimationClip> currentAnimationClips = new List<AnimationClip>();
        private static bool originalGridVisibility;
        private static bool originalGizmosVisibility;
        private static EditorApplication.CallbackFunction editorUpdateCallback;

        [MenuItem("Tools/Obstacle Animations/Preview Selected Animation", priority = 501)]
        [MenuItem("Assets/Obstacle Animations/Preview Selected Animation", priority = 11)]
        public static void PreviewSelectedAnimation()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[ObstacleAnimationPreviewer] Cannot preview while in Play Mode.");
                return;
            }

            // Cleanup previous previews if exist (call full cleanup)
            ClearPreview();

            // Get all selected sprite sheets
            var selectedObjects = Selection.objects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                Debug.LogError("[ObstacleAnimationPreviewer] No assets selected. Please select sprite sheet(s) in Project window.");
                return;
            }

            // Filter valid PNG sprite sheets
            var validSpriteSheets = new List<string>();
            foreach (var obj in selectedObjects)
            {
                var assetPath = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".png"))
                {
                    validSpriteSheets.Add(assetPath);
                }
            }

            if (validSpriteSheets.Count == 0)
            {
                Debug.LogError("[ObstacleAnimationPreviewer] No PNG sprite sheets selected. Please select obstacle sprite sheet(s).");
                return;
            }

            // Hide all existing objects in scene
            var allRootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var obj in allRootObjects)
            {
                obj.SetActive(false);
            }

            // Create preview data for all valid sprite sheets
            var previewDataList = new List<PreviewData>();
            foreach (var assetPath in validSpriteSheets)
            {
                var fileName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                
                var animationClip = FindAnimationClip(fileName);
                if (animationClip == null)
                {
                    Debug.LogWarning($"[ObstacleAnimationPreviewer] Animation clip not found for '{fileName}'. Skipping.");
                    continue;
                }

                var prefabPath = GetPrefabPath(fileName);
                if (string.IsNullOrEmpty(prefabPath))
                {
                    Debug.LogWarning($"[ObstacleAnimationPreviewer] Could not determine prefab type for '{fileName}'. Skipping.");
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    Debug.LogWarning($"[ObstacleAnimationPreviewer] Prefab not found at path: {prefabPath}. Skipping.");
                    continue;
                }

                previewDataList.Add(new PreviewData
                {
                    FileName = fileName,
                    AnimationClip = animationClip,
                    Prefab = prefab
                });
            }

            if (previewDataList.Count == 0)
            {
                Debug.LogError("[ObstacleAnimationPreviewer] No valid previews could be created.");
                // Restore visibility
                foreach (var obj in allRootObjects)
                {
                    obj.SetActive(true);
                }
                return;
            }

            // Create instances and collect animated objects for framing
            var animatedObjects = new List<GameObject>();
            var createdInstances = CreatePreviewInstances(previewDataList, out animatedObjects);
            
            if (createdInstances.Count == 0)
            {
                Debug.LogError("[ObstacleAnimationPreviewer] Failed to create preview instances.");
                // Restore visibility
                foreach (var obj in allRootObjects)
                {
                    obj.SetActive(true);
                }
                return;
            }

            currentPreviewInstances.AddRange(createdInstances);

            Debug.Log($"[ObstacleAnimationPreviewer] Using AnimationMode API for {animatedObjects.Count} objects");

            // Select all animated objects for framing
            Selection.objects = animatedObjects.ToArray();

            // Store animation data for callback
            currentAnimators.Clear();
            currentAnimatedObjects.Clear();
            currentAnimationClips.Clear();
            
            for (int i = 0; i < animatedObjects.Count; i++)
            {
                var obj = animatedObjects[i];
                if (obj == null) continue;
                
                var animator = obj.GetComponent<Animator>();
                if (animator != null)
                {
                    currentAnimators.Add(animator);
                    currentAnimatedObjects.Add(obj);
                    currentAnimationClips.Add(previewDataList[i].AnimationClip);
                }
            }

            // Start AnimationMode for editor preview
            AnimationMode.StartAnimationMode();
            
            // Setup editor update callback to keep animations running
            if (editorUpdateCallback != null)
            {
                EditorApplication.update -= editorUpdateCallback;
            }
            
            float animationTime = 0f;
            double lastUpdateTime = EditorApplication.timeSinceStartup;
            
            editorUpdateCallback = () =>
            {
                if (currentAnimatedObjects.Count == 0) return;
                
                // Calculate delta time since last update
                double currentTime = EditorApplication.timeSinceStartup;
                float deltaTime = (float)(currentTime - lastUpdateTime);
                lastUpdateTime = currentTime;
                
                // Advance animation time
                animationTime += deltaTime;
                
                // Sample all animations at current time
                for (int i = 0; i < currentAnimatedObjects.Count; i++)
                {
                    var obj = currentAnimatedObjects[i];
                    var clip = currentAnimationClips[i];
                    
                    if (obj == null || clip == null) continue;
                    
                    // Loop animation - clip.length is calculated from frameCount / frameRate
                    float time = animationTime % clip.length;
                    AnimationMode.SampleAnimationClip(obj, clip, time);
                }
                
                // Repaint scene view to show animation updates
                if (SceneView.lastActiveSceneView != null)
                {
                    SceneView.lastActiveSceneView.Repaint();
                }
            };
            EditorApplication.update += editorUpdateCallback;

            // Setup scene view and frame all objects
            EditorApplication.delayCall += () =>
            {
                if (animatedObjects.Count == 0) return;
                
                var sceneView = SceneView.lastActiveSceneView;
                if (sceneView == null) return;
                
                // Store original states
                originalGizmosVisibility = sceneView.drawGizmos;
                originalGridVisibility = sceneView.showGrid;
                
                // Hide gizmos and grid for clean preview
                sceneView.drawGizmos = false;
                sceneView.showGrid = false;
                
                // Keep selection active for framing
                Selection.objects = animatedObjects.ToArray();
                
                // Wait another frame for selection to fully register, then frame
                EditorApplication.delayCall += () =>
                {
                    if (animatedObjects.Count == 0 || animatedObjects.Any(obj => obj == null)) return;
                    
                    var sv = SceneView.lastActiveSceneView;
                    if (sv == null) return;
                    
                    // Calculate combined bounds of all objects
                    Bounds combinedBounds = new Bounds();
                    bool boundsInitialized = false;
                    
                    foreach (var obj in animatedObjects)
                    {
                        if (obj == null) continue;
                        var spriteRenderer = obj.GetComponent<SpriteRenderer>();
                        if (spriteRenderer != null && spriteRenderer.sprite != null)
                        {
                            if (!boundsInitialized)
                            {
                                combinedBounds = spriteRenderer.bounds;
                                boundsInitialized = true;
                            }
                            else
                            {
                                combinedBounds.Encapsulate(spriteRenderer.bounds);
                            }
                        }
                    }
                    
                    if (boundsInitialized)
                    {
                        sv.Frame(combinedBounds, false);
                    }
                    else
                    {
                        sv.FrameSelected();
                    }
                    
                    // Deselect after framing
                    EditorApplication.delayCall += () =>
                    {
                        Selection.activeGameObject = null;
                    };
                    
                    sv.Repaint();
                };
                
                sceneView.Repaint();
            };

            Debug.Log($"[ObstacleAnimationPreviewer] Created {createdInstances.Count} preview(s)");
        }

        private struct PreviewData
        {
            public string FileName;
            public AnimationClip AnimationClip;
            public GameObject Prefab;
        }

        private static List<GameObject> CreatePreviewInstances(List<PreviewData> previewDataList, out List<GameObject> animatedObjects)
        {
            animatedObjects = new List<GameObject>();
            var instances = new List<GameObject>();

            // Calculate grid layout positions
            var positions = CalculateGridPositions(previewDataList);

            for (int i = 0; i < previewDataList.Count; i++)
            {
                var data = previewDataList[i];
                var position = positions[i];

                // Instantiate prefab properly for Editor
                var instance = PrefabUtility.InstantiatePrefab(data.Prefab) as GameObject;
                if (instance == null)
                {
                    Debug.LogWarning($"[ObstacleAnimationPreviewer] Failed to instantiate prefab for '{data.FileName}'. Skipping.");
                    continue;
                }

                instance.name = $"Preview_{data.FileName}";
                instance.transform.position = position;

                // Find child object with Animator (naming varies: BigCitizenSprite, SmallCitizenSprite, BigNotAliveSprite)
                var animator = instance.GetComponentInChildren<Animator>();
                if (animator == null)
                {
                    Debug.LogWarning($"[ObstacleAnimationPreviewer] Animator not found in prefab '{data.Prefab.name}'. Skipping animation assignment for '{data.FileName}'.");
                    Object.DestroyImmediate(instance);
                    continue;
                }

                // Assign animation using AnimatorOverrideController
                AssignAnimationToAnimator(animator, data.AnimationClip);

                instances.Add(instance);
                animatedObjects.Add(animator.gameObject);
            }

            return instances;
        }

        private static List<Vector3> CalculateGridPositions(List<PreviewData> previewDataList)
        {
            var positions = new List<Vector3>();
            
            // Get sizes from first frame of each animation clip
            var objectSizes = new List<Vector2>();
            
            foreach (var data in previewDataList)
            {
                Vector2 size = Vector2.one; // Default fallback
                
                if (data.AnimationClip != null)
                {
                    // Get first sprite from animation
                    var bindings = AnimationUtility.GetObjectReferenceCurveBindings(data.AnimationClip);
                    var spriteBinding = bindings.FirstOrDefault(b => b.type == typeof(SpriteRenderer) && b.propertyName == "m_Sprite");
                    
                    if (spriteBinding.path != null)
                    {
                        var keyframes = AnimationUtility.GetObjectReferenceCurve(data.AnimationClip, spriteBinding);
                        if (keyframes != null && keyframes.Length > 0)
                        {
                            var firstSprite = keyframes[0].value as Sprite;
                            if (firstSprite != null)
                            {
                                // Use sprite's actual size in world units (pixels / pixelsPerUnit)
                                size = firstSprite.bounds.size;
                            }
                        }
                    }
                }
                
                objectSizes.Add(size);
            }
            
            // Calculate positions in grid
            // Start from top (Y=0), move down for each row
            float currentX = 0f;
            float currentRowY = 0f; // Top of current row
            float rowHeight = 0f;
            int currentRowIndex = 0;
            
            for (int i = 0; i < objectSizes.Count; i++)
            {
                var size = objectSizes[i];
                var objWidth = size.x;
                var objHeight = size.y;
                
                // Start new row if we exceed max objects per row
                if (i > 0 && i % MaxObjectsPerRow == 0)
                {
                    currentX = 0f;
                    // Move down to next row (subtract previous row height + spacing)
                    currentRowY -= rowHeight * SpacingMultiplier;
                    currentRowIndex++;
                    rowHeight = 0f;
                }
                
                // Track max height in current row
                rowHeight = Mathf.Max(rowHeight, objHeight);
                
                // Position object
                // X: offset by half width (pivot is center on X axis)
                // Y: currentRowY is top of row, sprite pivot is at bottom, so place at (top - height)
                var position = new Vector3(currentX + objWidth / 2f, currentRowY - objHeight, 0f);
                positions.Add(position);
                
                // Move X cursor for next object
                currentX += objWidth * SpacingMultiplier;
            }
            
            return positions;
        }

        private static void AssignAnimationToAnimator(Animator animator, AnimationClip animationClip)
        {
            if (animator == null || animationClip == null) return;

            var originalController = animator.runtimeAnimatorController as AnimatorController;
            if (originalController == null)
            {
                Debug.LogWarning("[ObstacleAnimationPreviewer] RuntimeAnimatorController is not an AnimatorController.");
                return;
            }

            var overrideController = new AnimatorOverrideController(originalController);
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrideController.GetOverrides(overrides);

            for (int i = 0; i < overrides.Count; i++)
            {
                if (overrides[i].Key.name == AnimatorPlaceholderClipName)
                {
                    overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, animationClip);
                    break;
                }
            }

            overrideController.ApplyOverrides(overrides);
            animator.runtimeAnimatorController = overrideController;
        }

        private static AnimationClip FindAnimationClip(string fileName)
        {
            var expectedPath = $"Assets/Animations/Obstacles/{fileName}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(expectedPath);
            
            if (clip != null)
            {
                return clip;
            }

            // Fallback: Search in all Animation folders
            var guids = AssetDatabase.FindAssets($"{fileName} t:AnimationClip");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip != null && clip.name == fileName)
                {
                    return clip;
                }
            }

            return null;
        }

        private static string GetPrefabPath(string fileName)
        {
            var lowerName = fileName.ToLowerInvariant();

            // big_alive: people (standing), or explicit category (snake_case or legacy camelCase)
            if (lowerName.Contains("_people_") || lowerName.Contains("_big_alive_") || lowerName.Contains("_bigalive_"))
            {
                return BigCitizenPrefabPath;
            }

            // small_alive: dogs, cats, or explicit category (snake_case or legacy camelCase)
            if (lowerName.Contains("_dog_") || lowerName.Contains("_cat_") || lowerName.Contains("_small_alive_") || lowerName.Contains("_smallalive_"))
            {
                return SmallCitizenPrefabPath;
            }

            // big_not_alive: cars, trucks, buses, or explicit category (snake_case or legacy camelCase)
            if (lowerName.Contains("_car_") || lowerName.Contains("_truck_") || lowerName.Contains("_bus_") || lowerName.Contains("_big_not_alive_") || lowerName.Contains("_bignotalive_"))
            {
                return BigNotAlivePrefabPath;
            }

            return null;
        }

        /// <summary>
        /// Clear preview by reloading current scene without saving changes.
        /// This ensures all temporary objects are removed even after recompilation.
        /// </summary>
        [MenuItem("Tools/Obstacle Animations/Clear Preview", priority = 502)]
        [MenuItem("Assets/Obstacle Animations/Clear Preview", priority = 12)]
        public static void ClearPreview()
        {
            // Stop animation update callback
            if (editorUpdateCallback != null)
            {
                EditorApplication.update -= editorUpdateCallback;
                editorUpdateCallback = null;
            }
            
            // Stop AnimationMode
            if (AnimationMode.InAnimationMode())
            {
                AnimationMode.StopAnimationMode();
            }
            
            currentAnimators.Clear();
            currentAnimatedObjects.Clear();
            currentAnimationClips.Clear();
            currentPreviewInstances.Clear();
            
            // Reload current scene without saving changes
            // This guarantees all temporary preview objects are removed
            var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.OpenScene(currentScene.path, OpenSceneMode.Single);
            
            Debug.Log("[ObstacleAnimationPreviewer] Preview cleared by reloading scene.");
        }
    }
}
#endif
