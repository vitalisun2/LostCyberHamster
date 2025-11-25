#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
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
        
        // Store current preview instance to cleanup on next preview
        private static GameObject currentPreviewInstance;
        private static bool originalGridVisibility;
        private static bool originalGizmosVisibility;

        [MenuItem("Tools/Obstacle Animations/Preview Selected Animation", priority = 501)]
        [MenuItem("Assets/Obstacle Animations/Preview Selected Animation", priority = 11)]
        public static void PreviewSelectedAnimation()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[ObstacleAnimationPreviewer] Cannot preview while in Play Mode.");
                return;
            }

            // Cleanup previous preview if exists
            if (currentPreviewInstance != null)
            {
                Object.DestroyImmediate(currentPreviewInstance);
                currentPreviewInstance = null;
            }

            // Get selected sprite sheet
            var selected = Selection.activeObject;
            if (selected == null)
            {
                Debug.LogError("[ObstacleAnimationPreviewer] No asset selected. Please select a sprite sheet in Project window.");
                return;
            }

            var assetPath = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".png"))
            {
                Debug.LogError("[ObstacleAnimationPreviewer] Selected asset is not a PNG sprite sheet. Please select an obstacle sprite sheet.");
                return;
            }

            // Extract base name from sprite sheet path
            // Example: "Assets/Content/locations/01_New_York/sprites/obstacle_new_york_people_1_idle.png"
            var fileName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            
            // Find matching animation clip
            var animationClip = FindAnimationClip(fileName);
            if (animationClip == null)
            {
                Debug.LogError($"[ObstacleAnimationPreviewer] Animation clip not found for '{fileName}'. Expected path: Assets/Animations/Obstacles/{fileName}.anim");
                return;
            }

            // Determine prefab type based on naming convention
            var prefabPath = GetPrefabPath(fileName);
            if (string.IsNullOrEmpty(prefabPath))
            {
                Debug.LogError($"[ObstacleAnimationPreviewer] Could not determine prefab type for '{fileName}'. Add category to naming convention (_people_, _dog_, _car_).");
                return;
            }

            // Load prefab
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[ObstacleAnimationPreviewer] Prefab not found at path: {prefabPath}");
                return;
            }

            // Hide all existing objects in scene (store original visibility)
            var allRootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var obj in allRootObjects)
            {
                obj.SetActive(false);
            }

            // Instantiate in current scene at visible position
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = new Vector3(0, -2, 0);
            instance.name = $"[PREVIEW] {fileName}";
            
            // Store reference for cleanup on next preview
            currentPreviewInstance = instance;
            
            // Mark scene as dirty but DON'T save (user can undo with Ctrl+Z)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(instance.scene);
            Undo.RegisterCreatedObjectUndo(instance, "Preview Animation");
            
            // Find child with Animator and SpriteRenderer
            var animator = instance.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogError($"[ObstacleAnimationPreviewer] Animator not found on prefab '{prefab.name}'.");
                Object.DestroyImmediate(instance);
                
                // Restore visibility
                foreach (var obj in allRootObjects)
                {
                    obj.SetActive(true);
                }
                return;
            }

            // Get the child GameObject that has both Animator and SpriteRenderer
            var animatedObject = animator.gameObject;
            var spriteRenderer = animatedObject.GetComponent<SpriteRenderer>();

            // Setup animation using AnimatorOverrideController
            var baseController = animator.runtimeAnimatorController as AnimatorController;
            if (baseController == null)
            {
                Debug.LogError($"[ObstacleAnimationPreviewer] Animator controller is not AnimatorController type.");
                Object.DestroyImmediate(instance);
                return;
            }

            var overrideController = new AnimatorOverrideController(baseController);
            overrideController.name = $"PreviewController_{fileName}";
            overrideController[AnimatorPlaceholderClipName] = animationClip;
            animator.runtimeAnimatorController = overrideController;
            
            // Enable animator
            animator.enabled = true;
            animator.Rebind();
            animator.Update(0f);

            // Set first frame sprite if available
            if (spriteRenderer != null && animationClip != null)
            {
                var bindings = AnimationUtility.GetObjectReferenceCurveBindings(animationClip);
                var spriteBinding = bindings.FirstOrDefault(b => b.type == typeof(SpriteRenderer) && b.propertyName == "m_Sprite");
                
                if (spriteBinding.path != null)
                {
                    var keyframes = AnimationUtility.GetObjectReferenceCurve(animationClip, spriteBinding);
                    if (keyframes != null && keyframes.Length > 0)
                    {
                        var firstSprite = keyframes[0].value as Sprite;
                        if (firstSprite != null)
                        {
                            spriteRenderer.sprite = firstSprite;
                        }
                    }
                }
            }

            // Select the CHILD object with animator (not root)
            Selection.activeGameObject = animatedObject;

            // Setup scene view after a frame to ensure sprite is loaded
            EditorApplication.delayCall += () =>
            {
                if (animatedObject == null) return;
                
                var sceneView = SceneView.lastActiveSceneView;
                if (sceneView == null) return;
                
                // Store original states
                originalGizmosVisibility = sceneView.drawGizmos;
                originalGridVisibility = sceneView.showGrid;
                
                // Hide gizmos and grid for clean preview
                sceneView.drawGizmos = false;
                sceneView.showGrid = false;
                
                // IMPORTANT: Keep selection active for FrameSelected to work
                Selection.activeGameObject = animatedObject;
                
                // Wait another frame for selection to fully register, then frame
                EditorApplication.delayCall += () =>
                {
                    if (animatedObject == null) return;
                    
                    var sv = SceneView.lastActiveSceneView;
                    if (sv == null) return;
                    
                    // Ensure selection is still active
                    Selection.activeGameObject = animatedObject;
                    
                    // Frame the object to fill viewport
                    if (spriteRenderer != null && spriteRenderer.sprite != null)
                    {
                        var bounds = spriteRenderer.bounds;
                        sv.Frame(bounds, false);
                    }
                    else
                    {
                        sv.FrameSelected();
                    }
                    
                    sv.Repaint();
                };
                
                sceneView.Repaint();
            };

            // Open Animation window and start playback after a delay
            EditorApplication.delayCall += () =>
            {
                if (animatedObject == null) return; // Check if object still exists
                
                // Ensure selection is the animated child object
                Selection.activeGameObject = animatedObject;
                
                // Open Animation window
                EditorApplication.ExecuteMenuItem("Window/Animation/Animation");
                
                // Wait one more frame for Animation window to initialize
                EditorApplication.delayCall += () =>
                {
                    if (animatedObject == null) return;
                    
                    var animationWindowType = System.Type.GetType("UnityEditor.AnimationWindow,UnityEditor");
                    if (animationWindowType != null)
                    {
                        var animationWindow = EditorWindow.GetWindow(animationWindowType);
                        if (animationWindow != null)
                        {
                            // Ensure window is focused
                            animationWindow.Focus();
                            
                            // Use reflection to access AnimationWindowState
                            var stateProperty = animationWindowType.GetProperty("state", 
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            
                            if (stateProperty != null)
                            {
                                var state = stateProperty.GetValue(animationWindow);
                                if (state != null)
                                {
                                    // Set playing to true
                                    var playingProperty = state.GetType().GetProperty("playing");
                                    if (playingProperty != null)
                                    {
                                        playingProperty.SetValue(state, true);
                                        animationWindow.Repaint();
                                        
                                        // NOW deselect after animation started - gizmos already hidden
                                        EditorApplication.delayCall += () =>
                                        {
                                            Selection.activeGameObject = null;
                                        };
                                    }
                                }
                            }
                        }
                    }
                };
            };

            Debug.Log($"[ObstacleAnimationPreviewer] Preview created for '{fileName}'\n" +
                      $"Prefab: {prefab.name}\n" +
                      $"Animation: {animationClip.name}\n" +
                      $"Frames: {Mathf.RoundToInt(animationClip.length * animationClip.frameRate)}");
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

            // bigAlive: people (standing)
            if (lowerName.Contains("_people_"))
            {
                return BigCitizenPrefabPath;
            }

            // smallAlive: dogs, cats
            if (lowerName.Contains("_dog_") || lowerName.Contains("_cat_"))
            {
                return SmallCitizenPrefabPath;
            }

            // bigNotAlive: cars, trucks, buses
            if (lowerName.Contains("_car_") || lowerName.Contains("_truck_") || lowerName.Contains("_bus_"))
            {
                return BigNotAlivePrefabPath;
            }

            return null;
        }

        /// <summary>
        /// Cleans up preview and restores scene state.
        /// Call this manually or it will be cleaned up on next preview.
        /// </summary>
        [MenuItem("Tools/Obstacle Animations/Clear Preview", priority = 502)]
        [MenuItem("Assets/Obstacle Animations/Clear Preview", priority = 12)]
        public static void ClearPreview()
        {
            if (currentPreviewInstance != null)
            {
                Object.DestroyImmediate(currentPreviewInstance);
                currentPreviewInstance = null;
                
                // Restore original scene objects visibility
                var allRootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
                foreach (var obj in allRootObjects)
                {
                    obj.SetActive(true);
                }
                
                // Restore grid and gizmos visibility
                if (SceneView.lastActiveSceneView != null)
                {
                    SceneView.lastActiveSceneView.drawGizmos = originalGizmosVisibility;
                    SceneView.lastActiveSceneView.showGrid = originalGridVisibility;
                    SceneView.lastActiveSceneView.Repaint();
                }
                
                Debug.Log("[ObstacleAnimationPreviewer] Preview cleared.");
            }
        }
    }
}
#endif
