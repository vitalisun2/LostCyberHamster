using System;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Actors
{
    /// <summary>
    /// Контракт синхронизации PolygonCollider2D с Physics Shape текущего sprite frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpritePhysicsShapeColliderSync : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private PolygonCollider2D _polygonCollider;

        public SpriteRenderer SpriteRenderer => _spriteRenderer;
        public PolygonCollider2D PolygonCollider => _polygonCollider;

        public void InitializeCache()
        {
            throw new NotImplementedException("Sprite physics shape cache is not implemented.");
        }

        public void ApplyCurrentSpriteShape()
        {
            throw new NotImplementedException("Sprite physics shape sync is not implemented.");
        }
    }
}
