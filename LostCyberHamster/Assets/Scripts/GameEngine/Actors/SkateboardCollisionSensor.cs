using System;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Actors
{
    /// <summary>
    /// Даёт skateboard actor сплошную область контакта независимо от physics shape sprite.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class SkateboardCollisionSensor : MonoBehaviour
    {
        [SerializeField] private CollisionController _collisionController;

        private void Awake()
        {
            if (_collisionController == null)
            {
                throw new MissingReferenceException(
                    "SkateboardCollisionSensor requires CollisionController.");
            }

            BoxCollider2D sensor = GetComponent<BoxCollider2D>();
            if (!sensor.isTrigger)
            {
                throw new InvalidOperationException(
                    "SkateboardCollisionSensor requires trigger BoxCollider2D.");
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            _collisionController.TryHandleSkateboardSensorCollision(other, isStay: false);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            _collisionController.TryHandleSkateboardSensorCollision(other, isStay: true);
        }
    }
}
