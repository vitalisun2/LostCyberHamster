using System;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Actors
{
    /// <summary>
    /// Переключает взаимоисключающие normal и skateboard actor-ветки Hamster.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HamsterActorSwitcher : MonoBehaviour
    {
        [SerializeField] private GameObject _normalActor;
        [SerializeField] private GameObject _skateboardActor;

        public GameObject NormalActor => _normalActor;
        public GameObject SkateboardActor => _skateboardActor;

        public bool IsSkateboardActive =>
            _skateboardActor != null && _skateboardActor.activeSelf;

        private void Awake()
        {
            Initialize();
        }

        /// <summary>
        /// Проверяет prefab refs и устанавливает безопасный начальный normal mode.
        /// </summary>
        public void Initialize()
        {
            ValidateReferences();
            ActivateNormal();
        }

        /// <summary>
        /// Включает normal actor и полностью выключает skateboard actor.
        /// </summary>
        public void ActivateNormal()
        {
            SetActors(normalActive: true);
        }

        /// <summary>
        /// Включает skateboard actor и полностью выключает normal actor.
        /// </summary>
        public void ActivateSkateboard()
        {
            SetActors(normalActive: false);
        }

        private void SetActors(bool normalActive)
        {
            // Сначала выключаем прежнюю ветку, чтобы два collider не пересекались.
            GameObject actorToDisable = normalActive ? _skateboardActor : _normalActor;
            if (actorToDisable.activeSelf)
                actorToDisable.SetActive(false);

            // Затем включаем целевой actor; повторный вызов остаётся idempotent.
            GameObject actorToEnable = normalActive ? _normalActor : _skateboardActor;
            if (!actorToEnable.activeSelf)
                actorToEnable.SetActive(true);
        }

        private void ValidateReferences()
        {
            if (_normalActor == null || _skateboardActor == null)
            {
                throw new MissingReferenceException(
                    "HamsterActorSwitcher requires normal and skateboard actor references.");
            }

            if (_normalActor == _skateboardActor)
            {
                throw new InvalidOperationException(
                    "HamsterActorSwitcher actor references must be different objects.");
            }
        }
    }
}
