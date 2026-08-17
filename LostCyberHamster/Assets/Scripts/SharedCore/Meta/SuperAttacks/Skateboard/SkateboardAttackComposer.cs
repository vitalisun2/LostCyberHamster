using System;
using Assets.Scripts.GameEngine.Skins;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
using UnityEngine;

namespace Vues.GameCore
{
    /// <summary>
    /// Собирает Skateboard runtime из владельцев gameplay, surface, visual и impact.
    /// </summary>
    internal static class SkateboardAttackComposer
    {
        private const float _secondsPerTimingFrame = 1f / 60f;

        public static SkateboardAttack Create(
            SuperAttackData data,
            Hamster hamster,
            GameManager gameManager,
            Camera gameCamera,
            ObstacleSpawner obstacleSpawner)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (hamster == null)
                throw new ArgumentNullException(nameof(hamster));
            if (obstacleSpawner == null)
                throw new ArgumentNullException(nameof(obstacleSpawner));

            var actorSwitcher = hamster.ActorSwitcher ??
                throw new MissingReferenceException("HamsterActorSwitcher is missing.");
            var surfaceController = hamster.SkateboardSurfaceController ??
                throw new MissingReferenceException("SkateboardSurfaceController is missing.");
            var visualHost = hamster.SkateboardSkinVisualHost ??
                throw new MissingReferenceException("Skateboard SkinVisualHost is missing.");

            // Surface и presentation получают свои явные runtime dependencies.
            surfaceController.Configure(obstacleSpawner);
            var visualSequence = new SkateboardVisualSequence(
                visualHost,
                SkateboardAttack.DefaultJumpDuration,
                hamster.SkateboardRideCycle,
                hamster.SkateboardRun2Speed,
                hamster.SkateboardRun3Speed);
            var landingImpact = new SkateboardLandingImpactRuntime(
                hamster,
                obstacleSpawner,
                gameManager,
                gameCamera,
                new CameraShakeController(
                    gameCamera,
                    delayAfterLandingImpact:
                    ToSeconds(hamster.SkateboardCameraShakeDelayAfterLandingImpactFrames)),
                waveDelayAfterLandingImpact:
                ToSeconds(hamster.SkateboardWaveDelayAfterLandingImpactFrames));

            // Attack принимает ownership listener-backed landing runtime.
            try
            {
                return new SkateboardAttack(
                    hamster,
                    actorSwitcher,
                    surfaceController,
                    visualSequence,
                    gameManager,
                    landingImpact,
                    data.UltaDuration,
                    data.UltaCharge);
            }
            catch
            {
                landingImpact.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Переводит настройку кадров при 60 FPS в runtime-секунды.
        /// </summary>
        private static float ToSeconds(int frames)
        {
            return frames * _secondsPerTimingFrame;
        }
    }
}
