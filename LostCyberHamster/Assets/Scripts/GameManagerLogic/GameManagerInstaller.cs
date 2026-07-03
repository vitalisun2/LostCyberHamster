using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts.GameManagerLogic
{
    public class GameManagerInstaller : MonoBehaviour
    {
        public List<GameObject> _installerObjects = new();

        private void Awake()
        {
            DebugManager.DiagStability("[GAME MANAGER INSTALLER] awake begin");
            var installers = _installerObjects.Select(installerObject => installerObject.GetComponent<IInstaller>()).ToList();

            var listenersInstaller = this.gameObject.GetComponent<ListenersInstaller>();
            installers.Add(listenersInstaller);

            for (int i = 0; i < installers.Count; i++)
            {
                installers[i].Install();
            }

            DebugManager.DiagStability($"[GAME MANAGER INSTALLER] awake completed installers={installers.Count}");
        }
    }
}
