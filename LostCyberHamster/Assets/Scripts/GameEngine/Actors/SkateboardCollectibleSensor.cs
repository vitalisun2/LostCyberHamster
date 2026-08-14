using System;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Actors
{
    /// <summary>
    /// Даёт skateboard actor сплошную область подбора collectibles независимо от physics shape sprite.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class SkateboardCollectibleSensor : MonoBehaviour
    {
        [SerializeField] private CollisionController _collisionController;

        private void Awake()
        {
            if (_collisionController == null)
            {
                throw new MissingReferenceException(
                    "SkateboardCollectibleSensor requires CollisionController.");
            }

            BoxCollider2D sensor = GetComponent<BoxCollider2D>();
            if (!sensor.isTrigger)
            {
                throw new InvalidOperationException(
                    "SkateboardCollectibleSensor requires trigger BoxCollider2D.");
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            _collisionController.TryCollectSkateboardCollectable(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            _collisionController.TryCollectSkateboardCollectable(other);
        }
    }
}
