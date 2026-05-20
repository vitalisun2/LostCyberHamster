#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Assets.Scripts.System.FeatureFlags;
using UnityEngine;

namespace Assets.Scripts.Debugging
{
    /// <summary>
    /// Simple MonoBehaviour to toggle the DayPart levels feature during development builds.
    /// Works with both legacy Input Manager and the new Input System.
    /// </summary>
    public class FeatureFlagToggle : MonoBehaviour
    {
#if ENABLE_INPUT_SYSTEM
        [SerializeField]
        private Key _toggleKey = Key.F6;
#elif ENABLE_LEGACY_INPUT_MANAGER
        [SerializeField]
        private KeyCode _toggleKey = KeyCode.F6;
#endif

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                var keyControl = keyboard[_toggleKey];
                if (keyControl != null && keyControl.wasPressedThisFrame)
                {
                    ToggleFeature();
                }
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(_toggleKey))
            {
                ToggleFeature();
            }
#endif
        }

        private static void ToggleFeature()
        {
            DayPartLevelsFeature.SetEnabled(!DayPartLevelsFeature.IsEnabled, persist: true);
        }
    }
}
