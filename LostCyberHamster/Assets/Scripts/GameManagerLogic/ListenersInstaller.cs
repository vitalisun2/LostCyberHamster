using System.Linq;
using UnityEngine;

namespace Assets.Scripts.GameManagerLogic
{
    [RequireComponent(typeof(GameManager))]
    public class ListenersInstaller : MonoBehaviour, IInstaller
    {
        public GameObject[] AdditionalListenerObjects;

        public void Install()
        {
            var gameManager = GetComponent<GameManager>();
            var listeners = GetComponentsInChildren<Listeners.IGameListener>(true);

            var additionalListeners = AdditionalListenerObjects
                .SelectMany(obj => obj.GetComponentsInChildren<Listeners.IGameListener>(true));

            listeners = listeners.Concat(additionalListeners).ToArray();

            foreach (var listener in listeners)
            {
                gameManager.AddListener(listener);
            }

        }
    }
}
