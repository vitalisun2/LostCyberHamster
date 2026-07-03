using UnityEngine;

namespace Assets.Scripts.System.Rendering
{
    /// <summary>
    /// Centralizes sprite assignment with the built-in Sprites/Default material so addressable-instantiated
    /// renderers always get a valid shader without duplicating setup logic across call sites.
    /// </summary>
    public static class SpriteRendererMaterialHelper
    {
        private static Material _spritesDefaultMaterial;

        public static void ApplySpriteWithDefaultMaterial(SpriteRenderer renderer, Sprite sprite)
        {
            if (renderer == null)
            {
                Debug.LogError("[SpriteRendererMaterialHelper] SpriteRenderer is null.");
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

        public static Material GetSpritesDefaultMaterial()
        {
            if (_spritesDefaultMaterial != null)
            {
                return _spritesDefaultMaterial;
            }

            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogWarning("[SpriteRendererMaterialHelper] Shader 'Sprites/Default' not found.");
                return null;
            }

            _spritesDefaultMaterial = new Material(shader);
            return _spritesDefaultMaterial;
        }
    }

    public static class EnvironmentLayerPlacement
    {
        public static float GetPivotYForBottom(Sprite sprite, float bottomY)
        {
            if (sprite == null)
            {
                Debug.LogError("[EnvironmentLayerPlacement] Sprite is null.");
                return bottomY;
            }

            return bottomY - sprite.bounds.min.y;
        }
    }
}
