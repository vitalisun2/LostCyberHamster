#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Scripts.GameEngine.Skins;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LostCyberHamster.Editor
{
    /// <summary>
    /// Собирает visual assets нового скина из default templates.
    /// </summary>
    internal static class SkinVisualContentSynchronizer
    {
        private const string ClipsFolderName = "clips";

        /// <summary>
        /// Создаёт normal/skateboard clips, controllers и prefabs нового скина.
        /// </summary>
        internal static Sprite CreateSkinVisuals(
            SkinData defaultSkin,
            string slug)
        {
            SkinVisual normalTemplate = LoadSkinVisual(
                defaultSkin.SkinVisualAddress);
            SkinVisual skateboardTemplate = LoadSkinVisual(
                defaultSkin.SkateboardSkinVisualAddress);
            string normalTemplateSpriteRoot =
                SkinVisualContentLayout.GetSpritePath(
                    SkinVisualContentLayout.GetSlug(
                        defaultSkin.SkinVisualAddress),
                    isSkateboard: false);
            string skateboardTemplateSpriteRoot =
                SkinVisualContentLayout.GetSpritePath(
                    SkinVisualContentLayout.GetSlug(
                        defaultSkin.SkateboardSkinVisualAddress),
                    isSkateboard: true);

            // Собираем оба режима только для нового slug.
            Sprite initialNormalSprite = SyncMode(
                normalTemplate,
                normalTemplateSpriteRoot,
                slug,
                SkinVisualContentLayout.GetVisualAddress(
                    slug,
                    isSkateboard: false),
                isSkateboard: false);
            SyncMode(
                skateboardTemplate,
                skateboardTemplateSpriteRoot,
                slug,
                SkinVisualContentLayout.GetVisualAddress(
                    slug,
                    isSkateboard: true),
                isSkateboard: true);
            return initialNormalSprite;
        }

        private static SkinVisual LoadSkinVisual(string address)
        {
            string prefabPath =
                SkinVisualContentLayout.GetVisualPrefabPath(address);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                prefabPath);
            SkinVisual visual = prefab != null
                ? prefab.GetComponent<SkinVisual>()
                : null;
            return visual ??
                   throw new InvalidOperationException(
                       $"Default SkinVisual prefab is missing: {prefabPath}.");
        }

        private static Sprite SyncMode(
            SkinVisual template,
            string templateSpriteRoot,
            string slug,
            string targetAddress,
            bool isSkateboard)
        {
            if (template.Mappings == null || template.Mappings.Count == 0)
            {
                throw new InvalidOperationException(
                    "Default SkinVisual has no action mappings.");
            }

            IReadOnlyDictionary<string, Sprite> sprites =
                LoadVariantSprites(slug, isSkateboard);
            List<AnimationClip> templateClips = template.Mappings
                .Select(mapping => mapping?.Clip)
                .Where(clip => clip != null)
                .Distinct()
                .ToList();
            foreach (Sprite sprite in CollectSprites(templateClips))
            {
                string key = GetSpriteKey(sprite, templateSpriteRoot);
                if (!sprites.ContainsKey(key))
                {
                    throw new InvalidOperationException(
                        $"Sprite '{key}' is missing for '{slug}'.");
                }
            }

            var targetClips = new Dictionary<AnimationClip, AnimationClip>();
            foreach (AnimationClip templateClip in templateClips)
            {
                targetClips.Add(
                    templateClip,
                    SyncClip(
                        templateClip,
                        templateSpriteRoot,
                        slug,
                        sprites,
                        isSkateboard));
            }

            AnimatorController controller = SyncController(
                template,
                slug,
                targetClips,
                isSkateboard);
            return SyncPrefab(
                template,
                targetAddress,
                controller,
                targetClips,
                sprites,
                templateSpriteRoot,
                isSkateboard);
        }

        private static IReadOnlyDictionary<string, Sprite> LoadVariantSprites(
            string slug,
            bool isSkateboard)
        {
            string spriteRoot = SkinVisualContentLayout.GetSpritePath(
                slug,
                isSkateboard);
            var sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets(
                         "t:Texture2D",
                         new[] { spriteRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (Sprite sprite in AssetDatabase.LoadAllAssetsAtPath(path)
                             .OfType<Sprite>())
                {
                    string key = GetSpriteKey(sprite, spriteRoot);
                    if (sprites.ContainsKey(key))
                    {
                        throw new InvalidOperationException(
                            $"Duplicate sprite '{key}' in {spriteRoot}.");
                    }

                    sprites.Add(key, sprite);
                }
            }

            if (sprites.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No sprites found for '{slug}' in {spriteRoot}.");
            }

            return sprites;
        }

        private static string GetSpriteKey(
            Sprite sprite,
            string spriteRoot)
        {
            string assetPath = AssetDatabase.GetAssetPath(sprite);
            string prefix = spriteRoot.TrimEnd('/') + "/";
            if (!assetPath.StartsWith(prefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Sprite '{assetPath}' is outside template root " +
                    $"'{spriteRoot}'.");
            }

            return $"{assetPath[prefix.Length..]}/{sprite.name}";
        }

        private static AnimationClip SyncClip(
            AnimationClip template,
            string templateSpriteRoot,
            string slug,
            IReadOnlyDictionary<string, Sprite> sprites,
            bool isSkateboard)
        {
            string animationRoot = SkinVisualContentLayout.GetAnimationPath(
                slug,
                isSkateboard);
            string clipsRoot = $"{animationRoot}/{ClipsFolderName}";
            EnsureFolder(clipsRoot);
            string targetPath = $"{clipsRoot}/{template.name}.anim";
            AnimationClip target = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                targetPath);
            if (target == null)
            {
                target = new AnimationClip();
                AssetDatabase.CreateAsset(target, targetPath);
            }

            EditorUtility.CopySerialized(template, target);
            target.name = template.name;
            foreach (EditorCurveBinding binding in
                     AnimationUtility.GetObjectReferenceCurveBindings(template))
            {
                ObjectReferenceKeyframe[] keyframes =
                    AnimationUtility.GetObjectReferenceCurve(template, binding);
                for (int index = 0; index < keyframes.Length; index++)
                {
                    if (keyframes[index].value is not Sprite templateSprite)
                        continue;

                    string key = GetSpriteKey(
                        templateSprite,
                        templateSpriteRoot);
                    if (!sprites.TryGetValue(key, out Sprite sprite))
                    {
                        throw new InvalidOperationException(
                            $"Sprite '{key}' is missing for '{slug}'.");
                    }

                    keyframes[index].value = sprite;
                }

                AnimationUtility.SetObjectReferenceCurve(
                    target,
                    binding,
                    keyframes);
            }

            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssetIfDirty(target);
            return target;
        }

        private static AnimatorController SyncController(
            SkinVisual template,
            string slug,
            IReadOnlyDictionary<AnimationClip, AnimationClip> targetClips,
            bool isSkateboard)
        {
            string root = SkinVisualContentLayout.GetAnimationPath(
                slug,
                isSkateboard);
            EnsureFolder(root);
            string path = $"{root}/{slug}-skin-visual.controller";
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ??
                AnimatorController.CreateAnimatorControllerAtPath(path);
            if (controller == null || controller.layers.Length != 1)
            {
                throw new InvalidOperationException(
                    $"AnimatorController must have one layer: {path}.");
            }

            Animator templateAnimator = template.GetComponent<Animator>();
            AnimatorController templateController =
                templateAnimator?.runtimeAnimatorController as AnimatorController;
            if (templateController == null ||
                templateController.layers.Length == 0)
            {
                throw new InvalidOperationException(
                    "Default SkinVisual AnimatorController is missing.");
            }

            EnsureSpeedParameter(controller, templateController);
            AnimatorStateMachine stateMachine =
                controller.layers[0].stateMachine;
            foreach (ChildAnimatorState childState in stateMachine.states)
                stateMachine.RemoveState(childState.state);

            AnimatorState firstState = null;
            var states = new Dictionary<string, AnimatorState>(
                StringComparer.Ordinal);
            foreach (SkinVisualActionMapping mapping in template.Mappings)
            {
                if (mapping?.Clip == null ||
                    states.ContainsKey(mapping.StateName))
                {
                    continue;
                }

                AnimatorState state = stateMachine.AddState(mapping.StateName);
                state.motion = targetClips[mapping.Clip];
                state.speedParameter = SkinVisual.SpeedParameterName;
                state.speedParameterActive = true;
                states.Add(mapping.StateName, state);
                firstState ??= state;
            }

            string defaultStateName =
                templateController.layers[0].stateMachine.defaultState?.name;
            stateMachine.defaultState =
                defaultStateName != null &&
                states.TryGetValue(defaultStateName, out AnimatorState defaultState)
                    ? defaultState
                    : firstState ?? throw new InvalidOperationException(
                        $"Default SkinVisual has no mapped states for '{slug}'.");
            EditorUtility.SetDirty(stateMachine);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            return controller;
        }

        private static void EnsureSpeedParameter(
            AnimatorController controller,
            AnimatorController templateController)
        {
            if (controller.parameters.Any(
                    parameter =>
                        parameter.name == SkinVisual.SpeedParameterName))
            {
                return;
            }

            AnimatorControllerParameter templateParameter =
                templateController.parameters.SingleOrDefault(parameter =>
                    parameter.name == SkinVisual.SpeedParameterName) ??
                throw new InvalidOperationException(
                    "Default SkinVisual speed parameter is missing.");
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = templateParameter.name,
                type = templateParameter.type,
                defaultBool = templateParameter.defaultBool,
                defaultFloat = templateParameter.defaultFloat,
                defaultInt = templateParameter.defaultInt,
            });
        }

        private static Sprite SyncPrefab(
            SkinVisual template,
            string targetAddress,
            AnimatorController controller,
            IReadOnlyDictionary<AnimationClip, AnimationClip> targetClips,
            IReadOnlyDictionary<string, Sprite> sprites,
            string templateSpriteRoot,
            bool isSkateboard)
        {
            string path =
                SkinVisualContentLayout.GetVisualPrefabPath(targetAddress);
            int separatorIndex = path.LastIndexOf('/');
            EnsureFolder(path[..separatorIndex]);
            bool existed =
                AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            GameObject root = existed
                ? PrefabUtility.LoadPrefabContents(path)
                : Object.Instantiate(template.gameObject);
            if (root == null)
            {
                throw new InvalidOperationException(
                    $"SkinVisual prefab cannot be loaded: {path}.");
            }

            try
            {
                root.name = Path.GetFileNameWithoutExtension(path);
                SkinVisual visual = root.GetComponent<SkinVisual>() ??
                                    root.AddComponent<SkinVisual>();
                Animator animator = root.GetComponent<Animator>() ??
                                    root.AddComponent<Animator>();
                SpriteRenderer renderer =
                    root.GetComponent<SpriteRenderer>() ??
                    root.AddComponent<SpriteRenderer>();
                if (template.GetComponent<Animator>() == null)
                {
                    throw new MissingComponentException(
                        "Default SkinVisual Animator is missing.");
                }

                SpriteRenderer templateRenderer = template.SpriteRenderer ??
                    throw new MissingComponentException(
                        "Default SkinVisual SpriteRenderer is missing.");

                List<SkinVisualActionMapping> mappings = template.Mappings
                    .Select(mapping => CopyMapping(mapping, targetClips))
                    .ToList();
                List<Sprite> physicsSprites = isSkateboard
                    ? MapPhysicsShapeSprites(
                        template,
                        templateSpriteRoot,
                        sprites)
                    : new List<Sprite>();
                string initialSpriteKey = GetSpriteKey(
                    templateRenderer.sprite,
                    templateSpriteRoot);
                if (!sprites.TryGetValue(
                        initialSpriteKey,
                        out Sprite initialSprite))
                {
                    throw new InvalidOperationException(
                        $"Initial sprite '{initialSpriteKey}' is missing.");
                }

                renderer.sprite = initialSprite;
                animator.runtimeAnimatorController = controller;
                visual.ConfigureEditor(
                    animator,
                    renderer,
                    mappings,
                    physicsSprites);
                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    path,
                    out bool success);
                if (!success)
                {
                    throw new InvalidOperationException(
                        $"SkinVisual prefab cannot be saved: {path}.");
                }

                return initialSprite;
            }
            finally
            {
                if (existed)
                    PrefabUtility.UnloadPrefabContents(root);
                else
                    Object.DestroyImmediate(root);
            }
        }

        private static List<Sprite> MapPhysicsShapeSprites(
            SkinVisual template,
            string templateSpriteRoot,
            IReadOnlyDictionary<string, Sprite> sprites)
        {
            var result = new List<Sprite>();
            foreach (Sprite templateSprite in template.PhysicsShapeSprites)
            {
                if (templateSprite == null)
                {
                    throw new InvalidOperationException(
                        "Default SkinVisual physics manifest contains null.");
                }

                string key = GetSpriteKey(
                    templateSprite,
                    templateSpriteRoot);
                if (!sprites.TryGetValue(key, out Sprite sprite))
                {
                    throw new InvalidOperationException(
                        $"Physics Shape sprite '{key}' is missing.");
                }

                result.Add(sprite);
            }

            return result;
        }

        private static SkinVisualActionMapping CopyMapping(
            SkinVisualActionMapping source,
            IReadOnlyDictionary<AnimationClip, AnimationClip> targetClips)
        {
            if (source?.Clip == null)
            {
                throw new InvalidOperationException(
                    "Default SkinVisual contains incomplete mapping.");
            }

            return new SkinVisualActionMapping
            {
                Action = source.Action,
                MatchAnyVariant = source.MatchAnyVariant,
                Variant = source.Variant,
                MatchAnyOutcome = source.MatchAnyOutcome,
                Outcome = source.Outcome,
                StateName = source.StateName,
                Clip = targetClips[source.Clip],
                Loop = source.Loop,
            };
        }

        private static List<Sprite> CollectSprites(
            IEnumerable<AnimationClip> clips)
        {
            var sprites = new List<Sprite>();
            var uniqueSprites = new HashSet<Sprite>();
            foreach (AnimationClip clip in clips)
            {
                foreach (EditorCurveBinding binding in
                         AnimationUtility.GetObjectReferenceCurveBindings(clip))
                {
                    foreach (Sprite sprite in AnimationUtility
                                 .GetObjectReferenceCurve(clip, binding)
                                 .Select(keyframe => keyframe.value)
                                 .OfType<Sprite>())
                    {
                        if (uniqueSprites.Add(sprite))
                            sprites.Add(sprite);
                    }
                }
            }

            return sprites;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int separatorIndex = path.LastIndexOf('/');
            if (separatorIndex <= 0)
            {
                throw new InvalidOperationException(
                    $"Invalid asset folder path: {path}.");
            }

            string parent = path[..separatorIndex];
            string folderName = path[(separatorIndex + 1)..];
            EnsureFolder(parent);
            string guid = AssetDatabase.CreateFolder(parent, folderName);
            if (string.IsNullOrWhiteSpace(guid))
            {
                throw new InvalidOperationException(
                    $"Cannot create asset folder: {path}.");
            }
        }
    }
}
#endif
