using System;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Actors
{
    /// <summary>
    /// Хранит ссылки на взаимоисключающие actor-ветки Hamster.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HamsterActorSwitcher : MonoBehaviour
    {
        [SerializeField] private GameObject _normalActor;
        [SerializeField] private GameObject _skateboardActor;

        public GameObject NormalActor => _normalActor;
        public GameObject SkateboardActor => _skateboardActor;

        public bool IsSkateboardActive =>
            throw new NotImplementedException("Skateboard actor switching is not implemented.");

        public void ActivateNormal()
        {
            throw new NotImplementedException("Normal actor activation is not implemented.");
        }

        public void ActivateSkateboard()
        {
            throw new NotImplementedException("Skateboard actor activation is not implemented.");
        }
    }
}
