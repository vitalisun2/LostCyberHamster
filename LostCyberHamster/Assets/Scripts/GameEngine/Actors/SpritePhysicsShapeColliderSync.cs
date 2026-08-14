using System;
using System.Collections.Generic;
using Assets.Scripts.GameEngine.Skins;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Actors
{
    /// <summary>
    /// Синхронизирует PolygonCollider2D с закешированным Physics Shape текущего sprite frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpritePhysicsShapeColliderSync : MonoBehaviour
    {
        [SerializeField] private SkinVisualHost _skinVisualHost;
        [SerializeField] private PolygonCollider2D _polygonCollider;

        private readonly Dictionary<Sprite, Vector2[][]> _shapeCache = new();
        private SkinVisual _visual;
        private SpriteRenderer _spriteRenderer;
        private Sprite _appliedSprite;

        public SpriteRenderer SpriteRenderer => _spriteRenderer;
        public PolygonCollider2D PolygonCollider => _polygonCollider;

        private void OnEnable()
        {
            ValidateReferences();
            _skinVisualHost.VisualChanged += BindVisual;
            BindVisual(_skinVisualHost.CurrentVisual);
        }

        private void LateUpdate()
        {
            if (_spriteRenderer != null && _spriteRenderer.sprite != _appliedSprite)
                ApplyCurrentSpriteShape();
        }

        private void OnDisable()
        {
            if (_skinVisualHost != null)
                _skinVisualHost.VisualChanged -= BindVisual;
        }

        /// <summary>
        /// Читает Physics Shape всех кадров текущего visual и сохраняет готовые paths.
        /// </summary>
        public void InitializeCache()
        {
            _shapeCache.Clear();
            _appliedSprite = null;

            if (_visual == null)
                return;

            IReadOnlyList<Sprite> sprites = _visual.PhysicsShapeSprites;
            for (int index = 0; index < sprites.Count; index++)
            {
                Sprite sprite = sprites[index];
                if (sprite != null && !_shapeCache.ContainsKey(sprite))
                    CacheShape(sprite);
            }
        }

        /// <summary>
        /// Применяет все закешированные paths текущего sprite к PolygonCollider2D.
        /// </summary>
        public void ApplyCurrentSpriteShape()
        {
            Sprite sprite = _spriteRenderer != null ? _spriteRenderer.sprite : null;
            if (sprite == null)
                return;

            // Неизвестный кадр читается один раз и остаётся в кеше текущего visual.
            if (!_shapeCache.TryGetValue(sprite, out Vector2[][] paths))
            {
                CacheShape(sprite);
                paths = _shapeCache[sprite];
            }

            // Обновляем число контуров и применяем каждый, включая отдельную форму доски.
            _polygonCollider.pathCount = paths.Length;
            for (int pathIndex = 0; pathIndex < paths.Length; pathIndex++)
                _polygonCollider.SetPath(pathIndex, paths[pathIndex]);

            _appliedSprite = sprite;
        }

        private void BindVisual(SkinVisual visual)
        {
            if (_visual == visual)
                return;

            _visual = visual;
            _spriteRenderer = visual != null ? visual.SpriteRenderer : null;
            InitializeCache();
            ApplyCurrentSpriteShape();
        }

        private void CacheShape(Sprite sprite)
        {
            int pathCount = sprite.GetPhysicsShapeCount();
            if (pathCount == 0)
            {
                throw new InvalidOperationException(
                    $"Sprite '{sprite.name}' has no custom Physics Shape.");
            }

            var paths = new Vector2[pathCount][];
            var points = new List<Vector2>();
            for (int pathIndex = 0; pathIndex < pathCount; pathIndex++)
            {
                points.Clear();
                sprite.GetPhysicsShape(pathIndex, points);
                paths[pathIndex] = points.ToArray();
            }

            _shapeCache.Add(sprite, paths);
        }

        private void ValidateReferences()
        {
            if (_skinVisualHost == null)
            {
                throw new MissingReferenceException(
                    "SpritePhysicsShapeColliderSync requires SkinVisualHost.");
            }

            if (_polygonCollider == null)
            {
                throw new MissingReferenceException(
                    "SpritePhysicsShapeColliderSync requires PolygonCollider2D.");
            }
        }
    }
}
